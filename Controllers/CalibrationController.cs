using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using GCOnline.Models;
using Newtonsoft.Json;

namespace GCOnline.Controllers
{
    public class CalibrationController : Controller
    {
        private GC_DBContext db = new GC_DBContext();

        public ActionResult Index()
        {
            var distinctList = from c in db.Calibrations
                                    group c by c.CalibrationName into g
                                    select g.FirstOrDefault();

            return View(distinctList.ToList());
        }

        public ActionResult GetCaliPeaks(List<CaliConc> cali)
        {
            DataModel data = new DataModel();

            Dictionary<double, List<Peak>> caliPeaks = data.GetPeaksForCalibration(cali);

            var currentCalis = from x in db.Calibrations
                       where x.Current == 1
                       select x;

            KeyValuePair<List<Calibration>, Dictionary<double, List<Peak>>> kvp = new KeyValuePair<List<Calibration>, Dictionary<double, List<Peak>>>(currentCalis.ToList(), caliPeaks);

            string json = JsonConvert.SerializeObject(kvp);

            return Json(json);
        }

        public ActionResult GetCaliCurve(List<CaliPoint> caliCurve)
        {
            DataModel data = new DataModel();

            List<Calibration> caliList = data.GetCalibrationResults(caliCurve);

            string json = JsonConvert.SerializeObject(caliList);

            return Json(json);
        }

        public ActionResult SaveCaliCurve(List<Calibration> cali)
        {
            DataModel data = new DataModel();

            data.SaveCalibrationCurve(cali);

            return RedirectToAction("Index");
        }

        public ActionResult Delete(string name)
        {
            ViewBag.CaliName = name;
            return View();
        }

        [HttpPost, ActionName("Delete")]
        public ActionResult DeleteConfirmed(string name)
        {
            var calis = from x in db.Calibrations
                        where x.CalibrationName == name
                        select x;

            foreach (Calibration c in calis.ToList())
            {
                Calibration cali = db.Calibrations.Find(c.CalibrationID);

                db.Calibrations.Remove(cali);
            }

            db.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}
