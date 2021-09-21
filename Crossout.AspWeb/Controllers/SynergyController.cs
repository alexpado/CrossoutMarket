﻿using Crossout.AspWeb.Helper;
using Crossout.AspWeb.Models.Items;
using Crossout.AspWeb.Models.Language;
using Crossout.AspWeb.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZicoreConnector.Zicore.Connector.Base;

namespace Crossout.AspWeb.Controllers
{
    public class SynergyController : Controller
    {
        SqlConnector sql = new SqlConnector(ConnectionType.MySql);

        [Route("/data/synergy/{id:int}")]
        public IActionResult Synergy(int id)
        {
            sql.Open(WebSettings.Settings.CreateDescription());

            DataService db = new DataService(sql);

            try
            {
                var model = db.SelectItemSynergy(id);

                return Json(model);
            }
            catch (Exception ex)
            {
                return StatusCode(500);
            }
        }
    }
}
