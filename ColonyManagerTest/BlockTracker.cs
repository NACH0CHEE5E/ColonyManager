﻿using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jobs;
using Pandaros.Settlers.Jobs.Roaming;
using Pandaros.Settlers.Models;
using Pipliz;
using Pipliz.JSON;

namespace Pandaros.Settlers.ColonyManagement
{
    [ModLoader.ModManager]
    public class BlockTracker
    {
        static QueueFactory<TrackedPosition> _recordPositionFactory = new QueueFactory<TrackedPosition>("RecordPositions", 1);
        static List<TrackedPosition> _queuedPositions = new List<TrackedPosition>();
        private static readonly byte[] _SOH = new[] { (byte)0x02 };

        public static object Managers { get; private set; }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnShouldKeepChunkLoaded, GameLoader.NAMESPACE + ".ColonyManager.BlockTracker.OnShouldKeepChunkLoaded")]
        public static void OnShouldKeepChunkLoaded(ChunkUpdating.KeepChunkLoadedData data)
        {
            lock(_queuedPositions)
            foreach (var iterator in _queuedPositions)
            {
                if (iterator.GetVector().IsWithinBounds(data.CheckedChunk.Position, data.CheckedChunk.Bounds))
                    data.Result = true;
            }
        }

        static BlockTracker()
        {
            _recordPositionFactory.DoWork += _recordPositionFactory_DoWork;
            _recordPositionFactory.Start();
        }

        public static void RewindPlayersBlocks(Players.Player player)
        {
            foreach (var colony in player.Colonies)
                if (colony.Owners.Length == 1)
                     RewindColonyBlocks(colony);

            Task.Run(() =>
            {
                var playerId = player.ID.ToString();

                try
                {
                    using (TrackedPositionContext db = new TrackedPositionContext())
                    {
                        foreach (var trackedPos in db.Positions.Where(p => p.PlayerId == playerId))
                        {
                            var oldest = db.Positions.Where(o => o.X == trackedPos.X && o.Y == trackedPos.Y && o.Z == trackedPos.Z && o.TimeTracked < trackedPos.TimeTracked).OrderBy(tp => tp.TimeTracked).FirstOrDefault();

                            if (oldest == default(TrackedPosition))
                                oldest = trackedPos;

                            if (!_queuedPositions.Any(pos => pos.Equals(oldest)))
                            {
                                lock (_queuedPositions)
                                    _queuedPositions.Add(oldest);

                                ChunkQueue.QueuePlayerRequest(oldest.GetVector().ToChunk(), player);
                            }
                        }

                        if (_queuedPositions.Count <= 0)
                            return;

                        System.Threading.Thread.Sleep(10000);

                        List<TrackedPosition> replaced = new List<TrackedPosition>();

                        foreach (var trackedPos in _queuedPositions)
                            if (ServerManager.TryChangeBlock(trackedPos.GetVector(), (ushort)trackedPos.BlockId) == EServerChangeBlockResult.Success)
                                replaced.Add(trackedPos);

                        lock (_queuedPositions)
                        {
                            db.Positions.RemoveRange(_queuedPositions);
                            foreach (var replace in replaced)
                                _queuedPositions.Remove(replace);
                        }
                        
                        db.SaveChanges();
                    }
                }
                catch (DbEntityValidationException e)
                {
                    ProcessEntityException(e);
                }
                catch (Exception ex)
                {
                    PandaLogger.LogError(ex);
                }
            });
        }


        public static async void RewindColonyBlocks(Colony colony)
        {
            foreach (var npc in colony.Followers.ToList())
            {
                npc.health = 0;

                if (npc.Job is IAreaJob areaJob)
                    AreaJobTracker.RemoveJob(areaJob);

                npc.ClearJob();
                npc.OnDeath();
            }

            RoamingJobManager.Objectives.Remove(colony);

            ServerManager.ColonyTracker.ColoniesLock.EnterWriteLock();
            ServerManager.ColonyTracker.ColoniesByID.Remove(colony.ColonyID);
            Colony newcolony = new Colony(colony.ColonyID);
            newcolony.Stockpile.AddEnumerable(from unresolved in ServerManager.WorldSettingsReadOnly.InitialStockpile
                                              select new InventoryItem(unresolved.type, unresolved.amount));
            ServerManager.ColonyTracker.ColoniesByID.Add(newcolony.ColonyID, newcolony);
            ServerManager.ColonyTracker.ColoniesLock.ExitWriteLock();
            ServerManager.ColonyTracker.Save();

            await Task.Run(() =>
            {
                try
                {
                    var colonyName = colony.Name;

                    using (TrackedPositionContext db = new TrackedPositionContext())
                    {
                        foreach (var trackedPos in db.Positions.Where(p => p.ColonyId == colonyName))
                        {
                            var oldest = db.Positions.Where(o => o.X == trackedPos.X && o.Y == trackedPos.Y && o.Z == trackedPos.Z && o.TimeTracked < trackedPos.TimeTracked).OrderBy(tp => tp.TimeTracked).FirstOrDefault();

                            if (oldest == default(TrackedPosition))
                                oldest = trackedPos;

                            if (!_queuedPositions.Any(pos => pos.Equals(oldest)))
                            {
                                lock (_queuedPositions)
                                    _queuedPositions.Add(oldest);

                                ChunkQueue.QueuePlayerRequest(oldest.GetVector().ToChunk(), colony.Owners.FirstOrDefault());
                            }
                        }

                        if (_queuedPositions.Count <= 0)
                            return;

                        System.Threading.Thread.Sleep(10000);

                        List<TrackedPosition> replaced = new List<TrackedPosition>();

                        foreach (var trackedPos in _queuedPositions)
                            if (ServerManager.TryChangeBlock(trackedPos.GetVector(), (ushort)trackedPos.BlockId) == EServerChangeBlockResult.Success)
                                replaced.Add(trackedPos);

                        lock (_queuedPositions)
                        {
                            db.Positions.RemoveRange(_queuedPositions);
                            foreach (var replace in replaced)
                                _queuedPositions.Remove(replace);
                        }
                        
                        db.SaveChanges();
                    }
                }
                catch (DbEntityValidationException e)
                {
                    ProcessEntityException(e);
                }
                catch (Exception ex)
                {
                    PandaLogger.LogError(ex);
                }
            });
        }

        private static void ProcessEntityException(DbEntityValidationException e)
        {
            foreach (var eve in e.EntityValidationErrors)
            {
                PandaLogger.Log(ChatColor.red, "Entity of type \"{0}\" in state \"{1}\" has the following validation errors:", eve.Entry.Entity.GetType().Name, eve.Entry.State);

                foreach (var ve in eve.ValidationErrors)
                    PandaLogger.Log(ChatColor.red, "- Property: \"{0}\", Error: \"{1}\"", ve.PropertyName, ve.ErrorMessage);
            }
        }

        private static void _recordPositionFactory_DoWork(object sender, TrackedPosition pos)
        {
            try
            {
                using (TrackedPositionContext db = new TrackedPositionContext())
                {
                    db.Positions.Add(pos);
                    db.SaveChanges();
                }
            }
            catch (DbEntityValidationException e)
            {
                ProcessEntityException(e);
                throw;
            }
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnQuitLate, GameLoader.NAMESPACE + ".ColonyManager.BlockTracker.OnQuitLate")]
        public static void OnQuitLate()
        {
            _recordPositionFactory.Dispose();
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnTryChangeBlock, GameLoader.NAMESPACE + ".ColonyManager.BlockTracker.OnTryChangeBlockUser")]
        public static void OnTryChangeBlockUser(ModLoader.OnTryChangeBlockData d)
        {
            if (d.RequestOrigin.AsPlayer != null &&
                d.RequestOrigin.AsPlayer.ID.type != NetworkID.IDType.Server &&
                d.RequestOrigin.AsPlayer.ID.type != NetworkID.IDType.Invalid)
            {
                _recordPositionFactory.Enqueue(new TrackedPosition()
                {
                    BlockId = d.TypeOld.ItemIndex,
                    X = d.Position.x,
                    Y = d.Position.y,
                    Z = d.Position.z,
                    TimeTracked = DateTime.UtcNow,
                    PlayerId = d.RequestOrigin.AsPlayer.ID.ToString()
                });
            }
            else if (d.RequestOrigin.AsColony != null)
            {
                _recordPositionFactory.Enqueue(new TrackedPosition()
                {
                    BlockId = d.TypeOld.ItemIndex,
                    X = d.Position.x,
                    Y = d.Position.y,
                    Z = d.Position.z,
                    TimeTracked = DateTime.UtcNow,
                    ColonyId = d.RequestOrigin.AsColony.Name
                });
            }
        }
    }
}
