using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using GCOnline.Models;
using System.IO;

namespace GCOnline.Controllers
{ 
    public class GCDataController : Controller
    {
        private GC_DBContext db = new GC_DBContext();

        public ViewResult Index()
        {
            return View(db.Sequences.ToList());
        }

        [HttpPost]
        public ActionResult Create(HttpPostedFileBase file)
        {
            DataModel data = new DataModel();

            data.UploadSequence(file);

            return RedirectToAction("Index");
        }

        public ActionResult GetRuns(FormCollection form)
        {
            List<List<Run>> runList = new List<List<Run>>();
            foreach (var key in form)
            {
                double id = Convert.ToDouble(key);

                var runs = from x in db.Runs
                           where x.SequenceID == id
                           select x;

                List<Run> r = runs.ToList();

                runList.Add(r);
            }

            return View(runList);
        }

        public ViewResult Details(FormCollection form)
        {
            List<Sequence> sequenceList = new List<Sequence>();

            foreach (var key in form)
            {
                double id = Convert.ToDouble(key);

                Sequence sequence = db.Sequences.Find(id);

                sequenceList.Add(sequence);
            }

            return View(sequenceList);
        }

        public ActionResult Delete(FormCollection form)
        {
            List<Sequence> sequenceList = new List<Sequence>();

            foreach (var key in form)
            {
                double id = Convert.ToDouble(key);

                Sequence sequence = db.Sequences.Find(id);

                sequenceList.Add(sequence);
            }

            return View(sequenceList);
        }

        [HttpPost, ActionName("Delete")]
        public ActionResult DeleteConfirmed(FormCollection form)
        {
            foreach (var key in form)
            {
                double id = Convert.ToDouble(key);

                Sequence sequence = db.Sequences.Find(id);

                db.Sequences.Remove(sequence);

                var runs = from x in db.Runs
                           where x.SequenceID == sequence.SequenceID
                           select x;

                foreach (Run r in runs)
                {
                    db.Runs.Remove(r);
                }

                var peaks = from x in db.Peaks
                            where x.SequenceID == sequence.SequenceID
                            select x;

                foreach (Peak p in peaks)
                {
                    db.Peaks.Remove(p);
                }
            }

            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }
    }
}