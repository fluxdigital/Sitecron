﻿using Cognifide.PowerShell;
using Cognifide.PowerShell.Core.Host;
using Cognifide.PowerShell.Core.Settings;
using Cognifide.PowerShell.Core.Extensions;
using Quartz;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecron.SitecronSettings;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace Sitecron.Jobs.PowerShell
{
    public class ExecuteScript : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            try
            {
                JobDataMap dataMap = context.JobDetail.JobDataMap;
                string contextDbName = Settings.GetSetting(SitecronConstants.SettingsNames.SiteCronContextDB);

                string scriptIDs = dataMap.GetString(SitecronConstants.FieldNames.Items);
                string rawParameters = dataMap.GetString(SitecronConstants.FieldNames.Parameters);

                Log.Info(string.Format("Sitecron: Powershell.ExecuteScript Instance {0} of ExecuteScript Job - {5}Parameters: {1} ScriptIDs: {2} {5}Fired at: {3} {5}Next Scheduled For:{4}", context.JobDetail.Key, rawParameters, scriptIDs, context.FireTimeUtc.Value.ToString("r"), context.NextFireTimeUtc.Value.ToString("r"), Environment.NewLine), this);

                if (!string.IsNullOrEmpty(scriptIDs))
                {
                    Database contextDb = Factory.GetDatabase(contextDbName);

                    List<Item> scriptItems = new List<Item>();
                    string id = scriptIDs.Split('|').FirstOrDefault(); //only doing the first script item, running multiple scripts can cause issues especially if they call other scripts etc.

                    Item scriptItem = contextDb.GetItem(new ID(id));

                    Log.Info(string.Format("Sitecron: Powershell.ExecuteScript: Adding Script: {0} {1}", scriptItem.Name, id), this);

                    NameValueCollection parameters = Sitecore.Web.WebUtil.ParseUrlParameters(rawParameters);

                    Run(scriptItem, parameters);
                }
                else
                    Log.Info("Sitecron: Powershell.ExecuteScript: No scripts found to execute!", this);
            }
            catch (Exception ex)
            {
                Log.Error("Sitecron: Powershell.ExecuteScript: ERROR something went wrong - " + ex.Message, this);
            }
        }


        private void Run(Item speScript, NameValueCollection parameters)
        {
            var script = speScript.Fields[FieldIDs.Script].Value ?? string.Empty;
            if (!string.IsNullOrEmpty(script))
            {
                Log.Info(string.Format("Sitecron: Powershell.ExecuteScript: Running Script: {0} {1}", speScript.Name, speScript.ID.ToString()), this);

                if (speScript.IsPowerShellScript())
                {
                    //reset session for each script otherwise the position of the items and env vars set by the previous script will be inherited by the subsequent scripts
                    using (var session = ScriptSessionManager.NewSession(ApplicationNames.Default, true))
                    {
                        //here we are passing the param collection to the script
                        var paramItems = parameters.AllKeys.SelectMany(parameters.GetValues, (k, v) => new { Key = k, Value = v });
                        foreach (var p in paramItems)
                        {
                            if (String.IsNullOrEmpty(p.Key)) continue;
                            if (String.IsNullOrEmpty(p.Value)) continue;

                            if (session.GetVariable(p.Key) == null)
                            {
                                session.SetVariable(p.Key, p.Value);
                            }
                        }

                        session.SetExecutedScript(speScript);
                        //session.SetItemLocationContext(speScript); //not needed anymore?
                        session.ExecuteScriptPart(script);
                    }
                }
            }

        }


        #region Multiple Scripts Scenario
        ////public void Execute(IJobExecutionContext context)
        ////{
        ////    JobDataMap dataMap = context.JobDetail.JobDataMap;

        ////    string scriptIDs = dataMap.GetString(FieldNames.Items);
        ////    string rawParameters = dataMap.GetString(FieldNames.Parameters);

        ////    Log.Info(string.Format("Sitecron: Powershell.ExecuteScript Instance {0} of ExecuteScript Job - {4}Parameters: {1} {4}Fired at: {2} {4}Next Scheduled For:{3}", context.JobDetail.Key, scriptParams, context.FireTimeUtc.Value.ToString("r"), context.NextFireTimeUtc.Value.ToString("r"), Environment.NewLine), this);

        ////    if (!string.IsNullOrEmpty(scriptIDs))
        ////    {
        ////        Database masterDb = Factory.GetDatabase(SitecronConstants.SitecoreDatabases.Master);

        ////        List<Item> scriptItems = new List<Item>();
        ////        string[] ids = scriptIDs.Split('|');
        ////        foreach (string id in ids)
        ////        {
        ////            scriptItems.Add(masterDb.GetItem(new ID(id)));
        ////            Log.Info(string.Format("Sitecron: Powershell.ExecuteScript: Adding Script: {0}", id), this);
        ////        }

        ////        NameValueCollection parameters = Sitecore.Web.WebUtil.ParseUrlParameters(rawParameters);

        ////        Run(scriptItems.ToArray(), parameters);
        ////    }
        ////    else
        ////        Log.Info("Sitecron: Powershell.ExecuteScript: No scripts found to execute!", this);
        ////}


        //////general bad practice here to allow multiple powershell scripts as there might be dependency
        //////of one script on another etc, should be one script per scheduled task
        //////this is shown as an example
        ////private void Run(Item[] speScripts, NameValueCollection parameters)
        ////{
        ////    foreach (var item in speScripts)
        ////    {
        ////        var script = item[ScriptItemFieldNames.Script];
        ////        if (!String.IsNullOrEmpty(script))
        ////        {
        ////            Log.Info(string.Format("Sitecron: Powershell.ExecuteScript: Running Script: {0} {1}", item.Name, item.ID.ToString()), this);

        ////            //reset session for each script otherwise the position of the items and env vars set by the previous script will be inherited by the subsequent scripts
        ////            using (var session = ScriptSessionManager.NewSession(ApplicationNames.Default, true))
        ////            {
        ////                //here we are passing the same param collection to all the scripts
        ////                var paramItems = parameters.AllKeys.SelectMany(parameters.GetValues, (k, v) => new { Key = k, Value = v });
        ////                foreach (var p in paramItems)
        ////                { 
        ////                    if (String.IsNullOrEmpty(p.Key)) continue;
        ////                    if (String.IsNullOrEmpty(p.Value)) continue;

        ////                    if (session.GetVariable(p.Key) == null)
        ////                    {
        ////                        session.SetVariable(p.Key, p.Value);
        ////                    }
        ////                }

        ////                session.SetExecutedScript(item);
        ////                session.SetItemLocationContext(item);
        ////                session.ExecuteScriptPart(script);
        ////            }
        ////        }
        ////    }
        ////}
        #endregion
    }
}