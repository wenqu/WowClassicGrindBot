﻿using System;
using System.Collections.Generic;
using System.Text;
using Libs.Actions;
using Libs.Addon;
using Libs.GOAP;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Libs.Actions
{
    public class ActionThread
    {
        private readonly ILogger logger;
        private readonly PlayerReader playerReader;
        private readonly WowProcess wowProcess;
        public readonly GoapAgent goapAgent;

        private GoapAction? currentAction;
        public bool Active { get; set; }

        public ActionThread(PlayerReader playerReader, WowProcess wowProcess, GoapAgent goapAgent, ILogger logger)
        {
            this.playerReader = playerReader;
            this.wowProcess = wowProcess;
            this.goapAgent = goapAgent;
            this.logger = logger;
        }

        public void OnActionEvent(object sender, ActionEvent e)
        {
            if (e.Key == GoapKey.abort)
            {
                logger.LogInformation($"Abort from: {sender.GetType().Name}");

                var location = this.playerReader.PlayerLocation;
                wowProcess?.Hearthstone();
                Active = false;
            }
        }

        public async Task GoapPerformAction()
        {
            if (this.goapAgent != null)
            {
                if (this.playerReader.PlayerBitValues.ItemsAreBroken)
                {
                    OnActionEvent(this, new ActionEvent(GoapKey.abort, true));
                }

                var newAction = await this.goapAgent.GetAction();

                if (newAction != null)
                {
                    if (newAction != this.currentAction)
                    {
                        this.currentAction?.DoReset();
                        this.currentAction = newAction;
                        logger.LogInformation("---------------------------------");
                        logger.LogInformation($"New Plan= {newAction.GetType().Name}");
                    }

                    try
                    {
                        await newAction.PerformAction();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"PerformAction on {newAction.GetType().Name}");
                    }
                }
                else
                {
                    logger.LogInformation($"New Plan= NULL");
                    Thread.Sleep(500);
                }
            }
        }
    }
}