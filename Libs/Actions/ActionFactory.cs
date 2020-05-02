﻿using Libs.Actions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Libs
{
    public class ActionFactory
    {
        private readonly AddonReader addonReader;
        private readonly NpcNameFinder npcNameFinder;
        private readonly WowProcess wowProcess;
        private ILogger logger;

        public bool PotentialAddsExist => npcNameFinder.PotentialAddsExist;

        public RouteInfo? RouteInfo { get; private set; }

        public ActionFactory(AddonReader addonReader, ILogger logger, WowProcess wowProcess, NpcNameFinder npcNameFinder)
        {
            this.logger = logger;
            this.addonReader = addonReader;
            this.npcNameFinder = npcNameFinder;
            this.wowProcess = wowProcess;
        }

        public HashSet<GoapAction> CreateActions(ClassConfiguration classConfig, Blacklist blacklist)    
        {
            var availableActions = new HashSet<GoapAction>();

            List<WowPoint> pathPoints, spiritPath;
            GetPaths(addonReader, out pathPoints, out spiritPath, classConfig);
            

            var playerDirection = new PlayerDirection(addonReader.PlayerReader, wowProcess, logger);
            var stopMoving = new StopMoving(wowProcess, addonReader.PlayerReader, logger);

            var stuckDetector = new StuckDetector(addonReader.PlayerReader, wowProcess, playerDirection, stopMoving, logger);
            var followRouteAction = new FollowRouteAction(addonReader.PlayerReader, wowProcess, playerDirection, pathPoints, stopMoving, npcNameFinder, blacklist, logger, stuckDetector, classConfig);
            var walkToCorpseAction = new WalkToCorpseAction(addonReader.PlayerReader, wowProcess, playerDirection, spiritPath, pathPoints, stopMoving, logger, stuckDetector);

            this.RouteInfo = new RouteInfo(pathPoints, spiritPath, followRouteAction, walkToCorpseAction);

            availableActions.Clear();

            availableActions.Add(followRouteAction);
            availableActions.Add(walkToCorpseAction);
            availableActions.Add(new TargetDeadAction(wowProcess, addonReader.PlayerReader, npcNameFinder, logger));
            availableActions.Add(new ApproachTargetAction(wowProcess, addonReader.PlayerReader, stopMoving, npcNameFinder, logger, stuckDetector, classConfig));

            if (classConfig.Loot)
            {
                availableActions.Add(new LootAction(wowProcess, addonReader.PlayerReader, addonReader.BagReader, stopMoving, logger, classConfig));
                availableActions.Add(new PostKillLootAction(wowProcess, addonReader.PlayerReader, addonReader.BagReader, stopMoving, logger, classConfig));
            }

            try
            {
                var genericCombat = new GenericCombatAction(wowProcess, addonReader.PlayerReader, stopMoving, logger, classConfig, playerDirection);
                availableActions.Add(genericCombat);
                availableActions.Add(new GenericPullAction(wowProcess, addonReader.PlayerReader, npcNameFinder, stopMoving, logger, genericCombat, classConfig, stuckDetector));

                foreach (var item in classConfig.Adhoc.Sequence)
                {
                    availableActions.Add(new AdhocAction(wowProcess, addonReader.PlayerReader, stopMoving, item, genericCombat, logger));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
            }

            return availableActions;
        }

        private void GetPaths(AddonReader addonReader, out List<WowPoint> pathPoints, out List<WowPoint> spiritPath, ClassConfiguration classConfig)
        {
            string pathText = File.ReadAllText(classConfig.PathFilename);
            bool thereAndBack = classConfig.PathThereAndBack;
            if (string.IsNullOrEmpty(classConfig.SpiritPathFilename))
            {
                classConfig.SpiritPathFilename = classConfig.PathFilename;
            }
            string spiritText = File.ReadAllText(classConfig.SpiritPathFilename);
            int step = classConfig.PathReduceSteps ? 2 : 1;

            var pathPoints2 = JsonConvert.DeserializeObject<List<WowPoint>>(pathText);

            pathPoints = new List<WowPoint>();
            for (int i = 0; i < pathPoints2.Count; i += step)
            {
                if (i < pathPoints2.Count)
                {
                    pathPoints.Add(pathPoints2[i]);
                }
            }

            if (thereAndBack)
            {
                var reversePoints = pathPoints.ToList();
                reversePoints.Reverse();
                pathPoints.AddRange(reversePoints);
            }

            pathPoints.Reverse();
            spiritPath = JsonConvert.DeserializeObject<List<WowPoint>>(spiritText);
        }
    }
}