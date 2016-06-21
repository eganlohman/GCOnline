using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using GCOnline.Models;
using Newtonsoft.Json;
using System.Collections;

namespace GCOnline.Controllers
{
    public class QuantificationController : Controller
    {
        private GC_DBContext db = new GC_DBContext();

        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Index(FormCollection form)
        {
            List<Run> runList = new List<Run>();

            DUMP list = new DUMP();

            foreach (var key in form)
            {
                Run newRun = db.Runs.Find(Convert.ToDouble(key));
                //newRun.CDW = newRun.CDW * .6;
                //newRun.SampleVolume = newRun.SampleVolume * .6;
                runList.Add(newRun);
            }

            list.rangeList = db.QuantificationRanges.ToList();

            var caliList = from c in db.Calibrations
                               group c by c.SequenceID into g
                               select g.FirstOrDefault();

            list.runList = runList;
            list.caliList = caliList.ToList();
            
            return View(list);
        }

        public ActionResult SaveQuantification(QuantificationRange range, List<Quantification> quantList)
        {
            DataModel data = new DataModel();

            QuantificationRange r = data.SaveQuantificationRange(range, quantList);

            string json = JsonConvert.SerializeObject(r);

            return Json(json);
        }

        public ActionResult Quantify(List<RunQuantification> runList)
        {
            DataModel data = new DataModel();

            data.SaveRunQuantification(runList);

            return View();
        }
    }
}
