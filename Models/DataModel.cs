using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using LinqToExcel;
using System.Data.Entity;
using System.Web.Mvc;
using System.ComponentModel.DataAnnotations;
using ExcelLibrary;
using ExcelLibrary.SpreadSheet;

namespace GCOnline.Models
{
    public class DataModel
    {
        private GC_DBContext db = new GC_DBContext();

        public void UploadSequence(HttpPostedFileBase file)
        {
            var excel = new ExcelQueryFactory("C:\\Users\\egan.lohman\\Documents\\Google Drive\\PHD_Research\\Research\\Algae\\SpreadSheets\\Chlamydomonas\\Restek_GC_Raw_Data\\" + file.FileName);
            IEnumerable<String> sheets = excel.GetWorksheetNames();

            int numberOfRuns = sheets.Count();

            var seq = from x in excel.Worksheet<Sequence>(0) select x;
            Sequence s = new Sequence();
            s.SequenceName = seq.ToList().ElementAt(0).SequenceName;
            s.Uploaded = DateTime.Now;
            s.NumberOfRuns = numberOfRuns;
            s.ExperimentDate = seq.ToList().ElementAt(0).ExperimentDate;
            db.Sequences.Add(s);
            db.SaveChanges();

            foreach (String sheet in sheets)
            {
                var run = from x in excel.Worksheet<Run>(sheet) select x;
                Run r = new Run();
                r.RunName = run.ToList().ElementAt(0).RunName;
                r.SequenceID = s.SequenceID;
                r.SequenceName = s.SequenceName;
                r.Dilution = run.ToList().ElementAt(0).Dilution;
                r.CDW = run.ToList().ElementAt(0).CDW;
                r.SampleVolume = run.ToList().ElementAt(0).SampleVolume;
                //r.CDWVolume = run.ToList().ElementAt(0).CDWVolume;
                r.BMCmmol = run.ToList().ElementAt(0).BMCmmol;
                r.CellCount = run.ToList().ElementAt(0).CellCount; 
                db.Runs.Add(r);
                db.SaveChanges();

                var rawdata = from x in excel.Worksheet<Peak>(sheet) select x;

                foreach(Peak p in rawdata) 
                {
                    p.SequenceID = s.SequenceID;
                    p.SequenceName = s.SequenceName;
                    p.RunID = r.RunID;
                    p.RunName = r.RunName;
                    db.Peaks.Add(p);
                }
            }

            db.SaveChanges();
        }

        internal void SaveCalibrationCurve(List<Calibration> calibrationList)
        {
            DateTime date = DateTime.Now;

            foreach (Calibration c in calibrationList) 
            {
                c.DateCreated = date;
                db.Calibrations.Add(c);
            }

            db.SaveChanges();
        }

        internal QuantificationRange SaveQuantificationRange(QuantificationRange range, List<Quantification> quantList)
        {
            QuantificationRange r = db.QuantificationRanges.Add(range);

            db.SaveChanges();

            foreach (Quantification q in quantList)
            {
                q.RangeID = r.RangeID;
                db.Quantifications.Add(q);
            }

            db.SaveChanges();

            return r;
        }

        internal void SaveRunQuantification(List<RunQuantification> runList)
        {
            List<RunQuantification> quantifiedRuns = new List<RunQuantification>();

            foreach (RunQuantification run in runList)
            {
                Run r = db.Runs.Find(run.RunID);
                Sequence s = db.Sequences.Find(r.SequenceID);

                //run.ExperimentDate = DateTime.FromOADate(Convert.ToDouble(s.ExperimentDate));
                run.ExperimentDate = s.ExperimentDate;

                var quants = from i in db.Quantifications
                             where i.RangeID == run.RangeID
                             select i;

                var peaks = from x in db.Peaks
                            where x.RunID == run.RunID
                            select x;

                Dictionary<string, List<Peak>> peaksPerStd = new Dictionary<string, List<Peak>>();

                foreach (Quantification q in quants.ToList())
                {
                    foreach(Peak p in peaks.ToList()) 
                    {
                        if (p.Time >= q.RT_Start && p.Time <= q.RT_End)
                        {
                            if (!peaksPerStd.ContainsKey(q.Compound))
                            {
                                peaksPerStd.Add(q.Compound, new List<Peak>());
                            }
                            peaksPerStd[q.Compound].Add(p); 
                        }
                    }
                }

                quantifiedRuns.Add(QuantifyRun(run, peaksPerStd));
            }

            GenerateExcelSpreadSheet(quantifiedRuns);
        }

        private RunQuantification QuantifyRun(RunQuantification run, Dictionary<string, List<Peak>> peaksPerStd)
        {
            var calis = from x in db.Calibrations
                        where x.CalibrationName == run.CalibrationName
                        select x;

            var range = from i in db.QuantificationRanges where i.RangeID == run.RangeID select i;
            QuantificationRange qr = range.ToList().ElementAt(0);

            Dictionary<string, double> lipidsPerStd = new Dictionary<string, double>();

            foreach (Calibration c in calis.ToList())
            {
                if (peaksPerStd.ContainsKey(c.Compound))
                {
                    List<Peak> peaks = peaksPerStd[c.Compound];

                    double totalLipidPerStd = 0;

                    foreach (Peak p in peaks)
                    {
                        p.Concentration = p.Area / c.Slope;

                        totalLipidPerStd += p.Concentration;
                    }

                    lipidsPerStd.Add(c.Compound, totalLipidPerStd);
                }
            }

            Dictionary<string, StdBeta> lookup = new Dictionary<string, StdBeta>()
            {
                {"C10_FFA",     new StdBeta(){Label = "C10 FFA", MW = 141, NumC = 10}},
                {"C12_FFA",     new StdBeta(){Label = "C12 FFA", MW = 169, NumC = 12}},
                {"C14_FFA",     new StdBeta(){Label = "C14 FFA", MW = 197, NumC = 14}},
                {"C16_FFA",     new StdBeta(){Label = "C16 FFA", MW = 225, NumC = 16}},
                {"C18_FFA",     new StdBeta(){Label = "C18 FFA", MW = 253, NumC = 18}},
                {"C20_FFA",     new StdBeta(){Label = "C20 FFA", MW = 281, NumC = 20}},
                {"C12_MAG",     new StdBeta(){Label = "C12 MAG", MW = 169, NumC = 12}},
                {"C14_MAG",     new StdBeta(){Label = "C14 MAG", MW = 197, NumC = 14}},
                {"C16_MAG",     new StdBeta(){Label = "C16 MAG", MW = 225, NumC = 16}},
                {"C18_MAG",     new StdBeta(){Label = "C18 MAG", MW = 253, NumC = 18}},
                {"C12_DAG",     new StdBeta(){Label = "C12 DAG", MW = 338, NumC = 24}},
                {"C14_DAG",     new StdBeta(){Label = "C14 DAG", MW = 394, NumC = 28}},
                {"C16_DAG",     new StdBeta(){Label = "C16 DAG", MW = 450, NumC = 32}},
                {"C18_DAG",     new StdBeta(){Label = "C18 DAG", MW = 506, NumC = 36}},
                {"C12_TAG",     new StdBeta(){Label = "C12 TAG", MW = 507, NumC = 36}},
                {"C14_TAG",     new StdBeta(){Label = "C14 TAG", MW = 591, NumC = 42}},
                {"C16_TAG",     new StdBeta(){Label = "C16 TAG", MW = 675, NumC = 48}},
                {"C18_TAG",     new StdBeta(){Label = "C18 TAG", MW = 759, NumC = 54}},
                {"C20_TAG",     new StdBeta(){Label = "C20 TAG", MW = 843, NumC = 60}},
                {"C22_TAG",     new StdBeta(){Label = "C22 TAG", MW = 927, NumC = 66}}
            };

            double total_mgmL = 0;

            foreach (KeyValuePair<string, double> compound in lipidsPerStd)
            {
                total_mgmL += compound.Value;
            }

            run.CompData = new List<CompoundData>();

            foreach (KeyValuePair<string, StdBeta> compound in lookup)
            {
                double conc_mgmL = 0;

                if(lipidsPerStd.ContainsKey(compound.Key))
                {
                    conc_mgmL = lipidsPerStd[compound.Key];
                }

                run.CompData.Add(new CompoundData()
                {
                    Compound = compound.Key,
                    Label = lookup[compound.Key].Label,
                    Conc_mgmL = conc_mgmL,
                    Conc_sv = (conc_mgmL * run.DilutionFactor) / run.SampleVolume,
                    Conc_ww = ((conc_mgmL * run.DilutionFactor) / (run.BMWeight * run.SampleVolume)) * 100,
                    Conc_cmmol = (((conc_mgmL * run.DilutionFactor) / run.SampleVolume) / (compound.Value.MW)) * compound.Value.NumC * 1000, // milliMol
                    Conc_1000cells = ((((conc_mgmL * run.DilutionFactor) / run.SampleVolume) * 1000000) / run.CellCount) * 1000, // picograms per 1000 cells
                    PercentTotal = (conc_mgmL / total_mgmL) * 100,
                }
                );
            }

            run.TotData = new TotalData();

            foreach (CompoundData cd in run.CompData)
            {
                if (cd.Label.Contains("FFA"))
                {
                    run.TotData.TotalFFA_mgmL += cd.Conc_mgmL;
                    run.TotData.TotalFFA_sv += cd.Conc_sv;
                    run.TotData.TotalFFA_ww += cd.Conc_ww;
                    run.TotData.TotalFFA_cmmol += cd.Conc_cmmol;
                    run.TotData.TotalFFA_1000cells += cd.Conc_1000cells;
                }
                if (cd.Label.Contains("MAG"))
                {
                    run.TotData.TotalMAG_mgmL += cd.Conc_mgmL;
                    run.TotData.TotalMAG_sv += cd.Conc_sv;
                    run.TotData.TotalMAG_ww += cd.Conc_ww;
                    run.TotData.TotalMAG_cmmol += cd.Conc_cmmol;
                    run.TotData.TotalMAG_1000cells += cd.Conc_1000cells;
                }
                if (cd.Label.Contains("DAG"))
                {
                    run.TotData.TotalDAG_mgmL += cd.Conc_mgmL;
                    run.TotData.TotalDAG_sv += cd.Conc_sv;
                    run.TotData.TotalDAG_ww += cd.Conc_ww;
                    run.TotData.TotalDAG_cmmol += cd.Conc_cmmol;
                    run.TotData.TotalDAG_1000cells += cd.Conc_1000cells;
                }
                if (cd.Label.Contains("TAG"))
                {
                    run.TotData.TotalTAG_mgmL += cd.Conc_mgmL;
                    run.TotData.TotalTAG_sv += cd.Conc_sv;
                    run.TotData.TotalTAG_ww += cd.Conc_ww;
                    run.TotData.TotalTAG_cmmol += cd.Conc_cmmol;
                    run.TotData.TotalTAG_1000cells += cd.Conc_1000cells;
                }

                run.TotData.TotalLipid_mgmL += cd.Conc_mgmL;
                run.TotData.TotalLipid_sv += cd.Conc_sv;
                run.TotData.TotalLipid_ww += cd.Conc_ww;
                run.TotData.TotalLipid_cmmol += cd.Conc_cmmol;
                run.TotData.TotalLipid_1000cells += cd.Conc_1000cells;
            }

            return run;
        }

        private void GenerateExcelSpreadSheet(List<RunQuantification> quantifiedRuns)
        {
            string dir = "C:\\Users\\egan.lohman\\Documents\\Google Drive\\PHD_Research\\Research\\Algae\\SpreadSheets\\Chlamydomonas\\Restek_GC_Processed_Data\\";
            string filepath = dir + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + "_Raw_Lipid_Data.xls";

            Workbook workbook = new Workbook();
            Worksheet worksheet = new Worksheet("Raw Data");

            int row = 2;
            int col = 0;

            foreach (RunQuantification run in quantifiedRuns)
            {
                worksheet.Cells[row, col] = new ExcelLibrary.SpreadSheet.Cell("Experiment Date");
                worksheet.Cells[row + 1, col] = new ExcelLibrary.SpreadSheet.Cell("Run Name");

                worksheet.Cells[row, col + 1] = new ExcelLibrary.SpreadSheet.Cell(run.ExperimentDate);
                worksheet.Cells[row + 1, col + 1] = new ExcelLibrary.SpreadSheet.Cell(run.RunName);

                worksheet.Cells[row, col + 3] = new ExcelLibrary.SpreadSheet.Cell("Dilution Factor");
                worksheet.Cells[row + 1, col + 3] = new ExcelLibrary.SpreadSheet.Cell("Sample Volume");
                worksheet.Cells[row + 2, col + 3] = new ExcelLibrary.SpreadSheet.Cell("Biomass Volume");
                worksheet.Cells[row + 3, col + 3] = new ExcelLibrary.SpreadSheet.Cell("Biomass Weight (mg -mL)");
                worksheet.Cells[row + 4, col + 3] = new ExcelLibrary.SpreadSheet.Cell("Biomass Weight (C-mmol)");
                worksheet.Cells[row + 5, col + 3] = new ExcelLibrary.SpreadSheet.Cell("Cell Count");

                worksheet.Cells[row, col + 4] = new ExcelLibrary.SpreadSheet.Cell(run.DilutionFactor);
                worksheet.Cells[row + 1, col + 4] = new ExcelLibrary.SpreadSheet.Cell(run.SampleVolume);
                worksheet.Cells[row + 2, col + 4] = new ExcelLibrary.SpreadSheet.Cell(run.BMVolume);
                worksheet.Cells[row + 3, col + 4] = new ExcelLibrary.SpreadSheet.Cell(run.BMWeight);
                worksheet.Cells[row + 4, col + 4] = new ExcelLibrary.SpreadSheet.Cell(run.BMWeightCmmol);
                worksheet.Cells[row + 5, col + 4] = new ExcelLibrary.SpreadSheet.Cell(Convert.ToDouble(run.CellCount));

                row = row + 7;

                worksheet.Cells[row, col] = new ExcelLibrary.SpreadSheet.Cell("Compound");	
                worksheet.Cells[row, col + 1] = new ExcelLibrary.SpreadSheet.Cell("Label");	
                worksheet.Cells[row, col + 2] = new ExcelLibrary.SpreadSheet.Cell("Conc: mg/mL (RAW)");	
                worksheet.Cells[row, col + 3] = new ExcelLibrary.SpreadSheet.Cell("Conc: mg/mL (Per Sample Volume)");	
                worksheet.Cells[row, col + 4] = new ExcelLibrary.SpreadSheet.Cell("Conc: % w/w (Per Biomass)");	
                worksheet.Cells[row, col + 5] = new ExcelLibrary.SpreadSheet.Cell("Conc: C-mmol");	
                worksheet.Cells[row, col + 6] = new ExcelLibrary.SpreadSheet.Cell("Conc: pg/1000 Cells");
                worksheet.Cells[row, col + 7] = new ExcelLibrary.SpreadSheet.Cell("% of Total Lipid");

                row = row + 1;

                foreach(CompoundData cd in run.CompData)
                {
                    worksheet.Cells[row, col] = new ExcelLibrary.SpreadSheet.Cell(cd.Compound);
                    worksheet.Cells[row, col + 1] = new ExcelLibrary.SpreadSheet.Cell(cd.Label);
                    worksheet.Cells[row, col + 2] = new ExcelLibrary.SpreadSheet.Cell(cd.Conc_mgmL);
                    worksheet.Cells[row, col + 3] = new ExcelLibrary.SpreadSheet.Cell(cd.Conc_sv);
                    worksheet.Cells[row, col + 4] = new ExcelLibrary.SpreadSheet.Cell(cd.Conc_ww);
                    worksheet.Cells[row, col + 5] = new ExcelLibrary.SpreadSheet.Cell(cd.Conc_cmmol);
                    worksheet.Cells[row, col + 6] = new ExcelLibrary.SpreadSheet.Cell(cd.Conc_1000cells);
                    worksheet.Cells[row, col + 7] = new ExcelLibrary.SpreadSheet.Cell(cd.PercentTotal);
                        
                    row++;
                }

                row = row + 1;

                worksheet.Cells[row, col + 1] = new ExcelLibrary.SpreadSheet.Cell("Total FFA");
                worksheet.Cells[row, col + 2] = new ExcelLibrary.SpreadSheet.Cell("Total MAG");
                worksheet.Cells[row, col + 3] = new ExcelLibrary.SpreadSheet.Cell("Total DAG");
                worksheet.Cells[row, col + 4] = new ExcelLibrary.SpreadSheet.Cell("Total TAG");
                worksheet.Cells[row, col + 5] = new ExcelLibrary.SpreadSheet.Cell("Total Lipid");

                worksheet.Cells[row + 1, col] = new ExcelLibrary.SpreadSheet.Cell("Conc: mg/mL (RAW)");
                worksheet.Cells[row + 2, col] = new ExcelLibrary.SpreadSheet.Cell("Conc: mg/mL (Per Sample Volume)");
                worksheet.Cells[row + 3, col] = new ExcelLibrary.SpreadSheet.Cell("Conc: % w/w (Per Biomass)");
                worksheet.Cells[row + 4, col] = new ExcelLibrary.SpreadSheet.Cell("Conc: C-mmol");
                worksheet.Cells[row + 5, col] = new ExcelLibrary.SpreadSheet.Cell("Conc: pg/1000 Cells");
                worksheet.Cells[row + 6, col] = new ExcelLibrary.SpreadSheet.Cell("% of Total Lipid");

                worksheet.Cells[row + 1, col + 1] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalFFA_mgmL);
                worksheet.Cells[row + 2, col + 1] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalFFA_sv);
                worksheet.Cells[row + 3, col + 1] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalFFA_ww);
                worksheet.Cells[row + 4, col + 1] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalFFA_cmmol);
                worksheet.Cells[row + 5, col + 1] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalFFA_1000cells);
                worksheet.Cells[row + 6, col + 1] = new ExcelLibrary.SpreadSheet.Cell((run.TotData.TotalFFA_mgmL / run.TotData.TotalLipid_mgmL) * 100);

                worksheet.Cells[row + 1, col + 2] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalMAG_mgmL);
                worksheet.Cells[row + 2, col + 2] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalMAG_sv);
                worksheet.Cells[row + 3, col + 2] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalMAG_ww);
                worksheet.Cells[row + 4, col + 2] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalMAG_cmmol);
                worksheet.Cells[row + 5, col + 2] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalMAG_1000cells);
                worksheet.Cells[row + 6, col + 2] = new ExcelLibrary.SpreadSheet.Cell((run.TotData.TotalMAG_mgmL / run.TotData.TotalLipid_mgmL) * 100);

                worksheet.Cells[row + 1, col + 3] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalDAG_mgmL);
                worksheet.Cells[row + 2, col + 3] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalDAG_sv);
                worksheet.Cells[row + 3, col + 3] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalDAG_ww);
                worksheet.Cells[row + 4, col + 3] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalDAG_cmmol);
                worksheet.Cells[row + 5, col + 3] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalDAG_1000cells);
                worksheet.Cells[row + 6, col + 3] = new ExcelLibrary.SpreadSheet.Cell((run.TotData.TotalDAG_mgmL / run.TotData.TotalLipid_mgmL) * 100);

                worksheet.Cells[row + 1, col + 4] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalTAG_mgmL);
                worksheet.Cells[row + 2, col + 4] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalTAG_sv);
                worksheet.Cells[row + 3, col + 4] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalTAG_ww);
                worksheet.Cells[row + 4, col + 4] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalTAG_cmmol);
                worksheet.Cells[row + 5, col + 4] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalTAG_1000cells);
                worksheet.Cells[row + 6, col + 4] = new ExcelLibrary.SpreadSheet.Cell((run.TotData.TotalTAG_mgmL / run.TotData.TotalLipid_mgmL) * 100);

                worksheet.Cells[row + 1, col + 5] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalLipid_mgmL);
                worksheet.Cells[row + 2, col + 5] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalLipid_sv);
                worksheet.Cells[row + 3, col + 5] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalLipid_ww);
                worksheet.Cells[row + 4, col + 5] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalLipid_cmmol);
                worksheet.Cells[row + 5, col + 5] = new ExcelLibrary.SpreadSheet.Cell(run.TotData.TotalLipid_1000cells);
                worksheet.Cells[row + 6, col + 5] = new ExcelLibrary.SpreadSheet.Cell((run.TotData.TotalLipid_mgmL / run.TotData.TotalLipid_mgmL) * 100);

                row = row + 9;

            }

            workbook.Worksheets.Add(worksheet);

            Dictionary<int, SortedDictionary<DateTime, List<RunQuantification>>> runsOrganizedByGroupAndTime = new Dictionary<int, SortedDictionary<DateTime, List<RunQuantification>>>();

            foreach(RunQuantification run in quantifiedRuns) 
            {
                int group = run.Group;

                if (!runsOrganizedByGroupAndTime.ContainsKey(group))
                {
                    runsOrganizedByGroupAndTime.Add(group, new SortedDictionary<DateTime, List<RunQuantification>>());
                }

                if (!runsOrganizedByGroupAndTime[group].ContainsKey(run.ExperimentDate))
                {
                    runsOrganizedByGroupAndTime[group].Add(run.ExperimentDate, new List<RunQuantification>());
                }

                runsOrganizedByGroupAndTime[group][run.ExperimentDate].Add(run);
            }

            workbook.Worksheets.Add(CompoundSpreadSheets(runsOrganizedByGroupAndTime, "Total", ""));
            workbook.Worksheets.Add(CompoundSpreadSheets(runsOrganizedByGroupAndTime, "", "mg per mL"));
            workbook.Worksheets.Add(CompoundSpreadSheets(runsOrganizedByGroupAndTime, "", "w per w"));
            workbook.Worksheets.Add(CompoundSpreadSheets(runsOrganizedByGroupAndTime, "", "pg per 1000 Cells"));

            //workbook.Worksheets.Add(CombinedPercentofTotal(runsOrganizedByGroupAndTime));

            workbook.Worksheets.Add(FMDTSpreadSheets(runsOrganizedByGroupAndTime, "Total", ""));
            workbook.Worksheets.Add(FMDTSpreadSheets(runsOrganizedByGroupAndTime, "", "mg per mL"));
            workbook.Worksheets.Add(FMDTSpreadSheets(runsOrganizedByGroupAndTime, "", "w per w"));
            workbook.Worksheets.Add(FMDTSpreadSheets(runsOrganizedByGroupAndTime, "", "pg per 1000 Cells"));

            workbook.Worksheets.Add(TotalLipidSpreadSheets(runsOrganizedByGroupAndTime, "", "mg per mL"));
            workbook.Worksheets.Add(TotalLipidSpreadSheets(runsOrganizedByGroupAndTime, "", "w per w"));
            workbook.Worksheets.Add(TotalLipidSpreadSheets(runsOrganizedByGroupAndTime, "", "pg per 1000 Cells"));

            workbook.Worksheets.Add(CDW(runsOrganizedByGroupAndTime, "mg -mL"));

            workbook.Save(filepath);

        }

        private Worksheet CDW(Dictionary<int, SortedDictionary<DateTime, List<RunQuantification>>> runs, string type)
        {
            string label = "CDW (" + type + ")";

            Worksheet worksheet = new Worksheet(label);

            int a_row = 2;
            int s_row = a_row + 3 + runs.ElementAt(0).Value.Count();
            int col = 0;

            foreach (KeyValuePair<int, SortedDictionary<DateTime, List<RunQuantification>>> runsByGroup in runs)
            {
                Dictionary<DateTime, Dictionary<string, List<double>>> totalValues = new Dictionary<DateTime, Dictionary<string, List<double>>>();

                foreach (KeyValuePair<DateTime, List<RunQuantification>> runsByDate in runsByGroup.Value)
                {
                    foreach (RunQuantification run in runsByDate.Value)
                    {
                        if (!totalValues.ContainsKey(runsByDate.Key))
                        {
                            totalValues.Add(runsByDate.Key, new Dictionary<string, List<double>>() 
                                { 
                                    { label, new List<double>() }
                                }
                            );
                        }

                        if (type == "mg -mL")
                        {
                            totalValues[runsByDate.Key][label].Add(run.BMWeight);
                        }
                        else
                        {
                            totalValues[runsByDate.Key][label].Add(run.BMWeightCmmol);
                        }
                    }
                }

                int a_row2 = a_row + 2;
                int s_row2 = s_row + 2;

                foreach (KeyValuePair<DateTime, Dictionary<string, List<double>>> cvs in totalValues)
                {
                    worksheet.Cells[a_row2, col] = new ExcelLibrary.SpreadSheet.Cell(cvs.Key);
                    worksheet.Cells[s_row2, col] = new ExcelLibrary.SpreadSheet.Cell(cvs.Key);

                    int index = 2;

                    foreach (KeyValuePair<string, List<double>> cv in cvs.Value)
                    {
                        worksheet.Cells[a_row + 1, col + index] = new ExcelLibrary.SpreadSheet.Cell(cv.Key, "Bold");
                        worksheet.Cells[s_row + 1, col + index] = new ExcelLibrary.SpreadSheet.Cell(cv.Key, "Bold");

                        worksheet.Cells[a_row2, col + index] = new ExcelLibrary.SpreadSheet.Cell(CalculateAverage(cv.Value));
                        worksheet.Cells[s_row2, col + index] = new ExcelLibrary.SpreadSheet.Cell(CalculateStdDev(cv.Value));

                        index++;
                    }

                    a_row2++;
                    s_row2++;
                }

                worksheet.Cells[a_row, col] = new ExcelLibrary.SpreadSheet.Cell("Group " + runsByGroup.Key + ": AVERAGE");
                worksheet.Cells[s_row, col] = new ExcelLibrary.SpreadSheet.Cell("Group " + runsByGroup.Key + ": STDEV");

                a_row++;
                s_row++;

                worksheet.Cells[a_row, col] = new ExcelLibrary.SpreadSheet.Cell("Date");
                worksheet.Cells[a_row, col + 1] = new ExcelLibrary.SpreadSheet.Cell("Time");
                worksheet.Cells[s_row, col] = new ExcelLibrary.SpreadSheet.Cell("Date");
                worksheet.Cells[s_row, col + 1] = new ExcelLibrary.SpreadSheet.Cell("Time");

                a_row = s_row2 + 2;
                s_row = a_row + 3 + runs.ElementAt(0).Value.Count();
            }

            return worksheet;
        }

        private Worksheet TotalLipidSpreadSheets(Dictionary<int, SortedDictionary<DateTime, List<RunQuantification>>> runs, string type, string units)
        {
            string label = "Total Lipid (" + units + ")";

            Worksheet worksheet = new Worksheet(label);

            int a_row = 2;
            int s_row = a_row + 3 + runs.ElementAt(0).Value.Count();
            int col = 0;

            foreach (KeyValuePair<int, SortedDictionary<DateTime, List<RunQuantification>>> runsByGroup in runs)
            {
                Dictionary<DateTime, Dictionary<string, List<double>>> totalValues = new Dictionary<DateTime, Dictionary<string, List<double>>>();

                foreach (KeyValuePair<DateTime, List<RunQuantification>> runsByDate in runsByGroup.Value)
                {
                    foreach (RunQuantification run in runsByDate.Value)
                    {
                        if (!totalValues.ContainsKey(runsByDate.Key))
                        {
                            totalValues.Add(runsByDate.Key, new Dictionary<string, List<double>>() 
                                { 
                                    { label, new List<double>() }
                                }
                            );
                        }

                        if (units == "mg per mL")
                        {
                            totalValues[runsByDate.Key][label].Add(run.TotData.TotalLipid_sv);
                        }
                        if (units == "w per w")
                        {
                            totalValues[runsByDate.Key][label].Add(run.TotData.TotalLipid_ww);
                        }
                        if (units == "pg per 1000 Cells")
                        {
                            totalValues[runsByDate.Key][label].Add(run.TotData.TotalLipid_1000cells);
                        }
                    }
                }

                int a_row2 = a_row + 2;
                int s_row2 = s_row + 2;

                foreach (KeyValuePair<DateTime, Dictionary<string, List<double>>> cvs in totalValues)
                {
                    worksheet.Cells[a_row2, col] = new ExcelLibrary.SpreadSheet.Cell(cvs.Key);
                    worksheet.Cells[s_row2, col] = new ExcelLibrary.SpreadSheet.Cell(cvs.Key);

                    int index = 2;

                    foreach (KeyValuePair<string, List<double>> cv in cvs.Value)
                    {
                        worksheet.Cells[a_row + 1, col + index] = new ExcelLibrary.SpreadSheet.Cell(cv.Key, "Bold");
                        worksheet.Cells[s_row + 1, col + index] = new ExcelLibrary.SpreadSheet.Cell(cv.Key, "Bold");

                        worksheet.Cells[a_row2, col + index] = new ExcelLibrary.SpreadSheet.Cell(CalculateAverage(cv.Value));
                        worksheet.Cells[s_row2, col + index] = new ExcelLibrary.SpreadSheet.Cell(CalculateStdDev(cv.Value));

                        index++;
                    }

                    a_row2++;
                    s_row2++;
                }

                worksheet.Cells[a_row, col] = new ExcelLibrary.SpreadSheet.Cell("Group " + runsByGroup.Key + ": AVERAGE");
                worksheet.Cells[s_row, col] = new ExcelLibrary.SpreadSheet.Cell("Group " + runsByGroup.Key + ": STDEV");

                a_row++;
                s_row++;

                worksheet.Cells[a_row, col] = new ExcelLibrary.SpreadSheet.Cell("Date");
                worksheet.Cells[a_row, col + 1] = new ExcelLibrary.SpreadSheet.Cell("Time");
                worksheet.Cells[s_row, col] = new ExcelLibrary.SpreadSheet.Cell("Date");
                worksheet.Cells[s_row, col + 1] = new ExcelLibrary.SpreadSheet.Cell("Time");

                a_row = s_row2 + 2;
                s_row = a_row + 3 + runs.ElementAt(0).Value.Count();
            }

            return worksheet;
        }

        private Worksheet FMDTSpreadSheets(Dictionary<int, SortedDictionary<DateTime, List<RunQuantification>>> runs, string type, string units)
        {
            string label = "FMDT (" + units + ")";
            if (units == "") { label = "FMDT % of Total Lipid"; }

            Worksheet worksheet = new Worksheet(label);

            int a_row = 2;
            int s_row = a_row + 3 + runs.ElementAt(0).Value.Count();
            int col = 0;

            foreach (KeyValuePair<int, SortedDictionary<DateTime, List<RunQuantification>>> runsByGroup in runs)
            {
                Dictionary<DateTime, Dictionary<string, List<double>>> totalValues = new Dictionary<DateTime, Dictionary<string, List<double>>>();

                foreach (KeyValuePair<DateTime, List<RunQuantification>> runsByDate in runsByGroup.Value)
                {
                    foreach (RunQuantification run in runsByDate.Value)
                    {
                        if (!totalValues.ContainsKey(runsByDate.Key))
                        {
                            totalValues.Add(runsByDate.Key, new Dictionary<string, List<double>>() 
                                { 
                                    { "FFA", new List<double>() }, 
                                    { "MAG", new List<double>() }, 
                                    { "DAG", new List<double>() },
                                    { "TAG", new List<double>() } 
                                }
                            );
                        }

                        if (units == "")
                        {
                            totalValues[runsByDate.Key]["FFA"].Add((run.TotData.TotalFFA_mgmL / run.TotData.TotalLipid_mgmL) * 100);
                            totalValues[runsByDate.Key]["MAG"].Add((run.TotData.TotalMAG_mgmL / run.TotData.TotalLipid_mgmL) * 100);
                            totalValues[runsByDate.Key]["DAG"].Add((run.TotData.TotalDAG_mgmL / run.TotData.TotalLipid_mgmL) * 100);
                            totalValues[runsByDate.Key]["TAG"].Add((run.TotData.TotalTAG_mgmL / run.TotData.TotalLipid_mgmL) * 100);
                        }
                        else  
                        { 
                            if (units == "pg per 1000 Cells")
                            {
                                totalValues[runsByDate.Key]["FFA"].Add(run.TotData.TotalFFA_1000cells);
                                totalValues[runsByDate.Key]["MAG"].Add(run.TotData.TotalMAG_1000cells);
                                totalValues[runsByDate.Key]["DAG"].Add(run.TotData.TotalDAG_1000cells);
                                totalValues[runsByDate.Key]["TAG"].Add(run.TotData.TotalTAG_1000cells);
                            }
                            if (units == "mg per mL")
                            {
                                totalValues[runsByDate.Key]["FFA"].Add(run.TotData.TotalFFA_sv);
                                totalValues[runsByDate.Key]["MAG"].Add(run.TotData.TotalMAG_sv);
                                totalValues[runsByDate.Key]["DAG"].Add(run.TotData.TotalDAG_sv);
                                totalValues[runsByDate.Key]["TAG"].Add(run.TotData.TotalTAG_sv);
                            }
                            if (units == "w per w")
                            {
                                totalValues[runsByDate.Key]["FFA"].Add(run.TotData.TotalFFA_ww);
                                totalValues[runsByDate.Key]["MAG"].Add(run.TotData.TotalMAG_ww);
                                totalValues[runsByDate.Key]["DAG"].Add(run.TotData.TotalDAG_ww);
                                totalValues[runsByDate.Key]["TAG"].Add(run.TotData.TotalTAG_ww);
                            }
                        }
                    }
                }

                int a_row2 = a_row + 2;
                int s_row2 = s_row + 2;

                foreach (KeyValuePair<DateTime, Dictionary<string, List<double>>> cvs in totalValues)
                {
                    worksheet.Cells[a_row2, col] = new ExcelLibrary.SpreadSheet.Cell(cvs.Key);
                    worksheet.Cells[s_row2, col] = new ExcelLibrary.SpreadSheet.Cell(cvs.Key);

                    int index = 2;

                    foreach (KeyValuePair<string, List<double>> cv in cvs.Value)
                    {
                        worksheet.Cells[a_row + 1, col + index] = new ExcelLibrary.SpreadSheet.Cell(cv.Key, "Bold");
                        worksheet.Cells[s_row + 1, col + index] = new ExcelLibrary.SpreadSheet.Cell(cv.Key, "Bold");

                        worksheet.Cells[a_row2, col + index] = new ExcelLibrary.SpreadSheet.Cell(CalculateAverage(cv.Value));
                        worksheet.Cells[s_row2, col + index] = new ExcelLibrary.SpreadSheet.Cell(CalculateStdDev(cv.Value));

                        index++;
                    }

                    a_row2++;
                    s_row2++;
                }

                worksheet.Cells[a_row, col] = new ExcelLibrary.SpreadSheet.Cell("Group " + runsByGroup.Key + ": AVERAGE");
                worksheet.Cells[s_row, col] = new ExcelLibrary.SpreadSheet.Cell("Group " + runsByGroup.Key + ": STDEV");

                a_row++;
                s_row++;

                worksheet.Cells[a_row, col] = new ExcelLibrary.SpreadSheet.Cell("Date");
                worksheet.Cells[a_row, col + 1] = new ExcelLibrary.SpreadSheet.Cell("Time");
                worksheet.Cells[s_row, col] = new ExcelLibrary.SpreadSheet.Cell("Date");
                worksheet.Cells[s_row, col + 1] = new ExcelLibrary.SpreadSheet.Cell("Time");

                a_row = s_row2 + 2;
                s_row = a_row + 3 + runs.ElementAt(0).Value.Count();
            }

            return worksheet;
        }

        private Worksheet CompoundSpreadSheets(Dictionary<int, SortedDictionary<DateTime, List<RunQuantification>>> runs, string type, string units)
        {
            Worksheet worksheet;

            if (units != "")
            {
                worksheet = new Worksheet("Compound (" + units + ")");
            }
            else
            {
                worksheet = new Worksheet("Compound % of Total Lipid");
            }

            worksheet.Cells[0, 0] = new ExcelLibrary.SpreadSheet.Cell("Experiment Start Date:");
            worksheet.Cells[0, 1] = new ExcelLibrary.SpreadSheet.Cell(runs.ElementAt(0).Value.ElementAt(0).Key.AddDays(-2));

            int a_row = 2;
            int s_row = a_row + 3 + runs.ElementAt(0).Value.Count();
            int col = 0;

            foreach (KeyValuePair<int, SortedDictionary<DateTime, List<RunQuantification>>> runsByGroup in runs)
            {
                Dictionary<DateTime, Dictionary<string, List<double>>> compoundValues = new Dictionary<DateTime, Dictionary<string, List<double>>>();

                foreach (KeyValuePair<DateTime, List<RunQuantification>> runsByDate in runsByGroup.Value)
                {
                    foreach (RunQuantification run in runsByDate.Value)
                    {
                        foreach (CompoundData cd in run.CompData)
                        {
                            if (!compoundValues.ContainsKey(runsByDate.Key))
                            {
                                compoundValues.Add(runsByDate.Key, new Dictionary<string, List<double>>());
                            }

                            if (!compoundValues[runsByDate.Key].ContainsKey(cd.Label))
                            {
                                compoundValues[runsByDate.Key].Add(cd.Label, new List<double>());
                            }

                            double value = 0;

                            if (units == "mg per mL") { value = cd.Conc_sv; }
                            if (units == "w per w") { value = cd.Conc_ww; }
                            if (units == "pg per 1000 Cells") { value = cd.Conc_1000cells; }
                            if (type == "Total") { value = cd.PercentTotal; }

                            compoundValues[runsByDate.Key][cd.Label].Add(value);
                        }
                    }
                }

                int a_row2 = a_row + 2;
                int s_row2 = s_row + 2;

                foreach(KeyValuePair<DateTime, Dictionary<string, List<double>>> cvs in compoundValues) 
                {
                    worksheet.Cells[a_row2, col] = new ExcelLibrary.SpreadSheet.Cell(cvs.Key);
                    worksheet.Cells[s_row2, col] = new ExcelLibrary.SpreadSheet.Cell(cvs.Key);

                    int index = 2;

                    foreach (KeyValuePair<string, List<double>> cv in cvs.Value) 
                    {
                        worksheet.Cells[a_row + 1, col + index] = new ExcelLibrary.SpreadSheet.Cell(cv.Key, "Bold");
                        worksheet.Cells[s_row + 1, col + index] = new ExcelLibrary.SpreadSheet.Cell(cv.Key, "Bold");

                        worksheet.Cells[a_row2, col + index] = new ExcelLibrary.SpreadSheet.Cell(CalculateAverage(cv.Value));
                        worksheet.Cells[s_row2, col + index] = new ExcelLibrary.SpreadSheet.Cell(CalculateStdDev(cv.Value));
                        
                        index++;
                    }

                    a_row2++;
                    s_row2++;
                }

                worksheet.Cells[a_row, col] = new ExcelLibrary.SpreadSheet.Cell("Group " + runsByGroup.Key + ": AVERAGE");
                worksheet.Cells[s_row, col] = new ExcelLibrary.SpreadSheet.Cell("Group " + runsByGroup.Key + ": STDEV");

                a_row++;
                s_row++;

                worksheet.Cells[a_row, col] = new ExcelLibrary.SpreadSheet.Cell("Date");
                worksheet.Cells[a_row, col + 1] = new ExcelLibrary.SpreadSheet.Cell("Time");
                worksheet.Cells[s_row, col] = new ExcelLibrary.SpreadSheet.Cell("Date");
                worksheet.Cells[s_row, col + 1] = new ExcelLibrary.SpreadSheet.Cell("Time");

                a_row = s_row2 + 2;
                s_row = a_row + 3 + runs.ElementAt(0).Value.Count();
            }

            return worksheet;
        }

        private Worksheet CombinedPercentofTotal(Dictionary<int, SortedDictionary<DateTime, List<RunQuantification>>> runs)
        {
            Worksheet worksheet = new Worksheet("Combined % of Total (C-mmol)");

            int a_row = 2;
            int s_row = a_row + 3 + runs.ElementAt(0).Value.Count();
            int col = 0;

            foreach (KeyValuePair<int, SortedDictionary<DateTime, List<RunQuantification>>> runsByGroup in runs)
            {
                Dictionary<DateTime, Dictionary<string, List<double>>> compoundValues = new Dictionary<DateTime, Dictionary<string, List<double>>>();

                foreach (KeyValuePair<DateTime, List<RunQuantification>> runsByDate in runsByGroup.Value)
                {
                    foreach (RunQuantification run in runsByDate.Value)
                    {
                        foreach (CompoundData cd in run.CompData)
                        {
                            if (!compoundValues.ContainsKey(runsByDate.Key))
                            {
                                compoundValues.Add(runsByDate.Key, new Dictionary<string, List<double>>());
                            }

                            if (!compoundValues[runsByDate.Key].ContainsKey(cd.Label))
                            {
                                compoundValues[runsByDate.Key].Add(cd.Label, new List<double>());
                            }

                            compoundValues[runsByDate.Key][cd.Label].Add(cd.PercentTotal);
                        }
                    }
                }

                int a_row2 = a_row + 2;
                int s_row2 = s_row + 2;

                foreach (KeyValuePair<DateTime, Dictionary<string, List<double>>> cvs in compoundValues)
                {
                    worksheet.Cells[a_row2, col] = new ExcelLibrary.SpreadSheet.Cell(cvs.Key);
                    worksheet.Cells[s_row2, col] = new ExcelLibrary.SpreadSheet.Cell(cvs.Key);

                    Dictionary<string, List<List<double>>> valuesByCL = new Dictionary<string, List<List<double>>>();

                    foreach (KeyValuePair<string, List<double>> cv in cvs.Value)
                    {
                        string label = "C" + cv.Key.Substring(1,2);

                        if (!valuesByCL.ContainsKey(label))
                        {
                            valuesByCL.Add(label, new List<List<double>>());
                        }

                        valuesByCL[label].Add(cv.Value);
                    }

                    Dictionary<string, List<double>> averagesByCL = new Dictionary<string, List<double>>();

                    foreach (KeyValuePair<string, List<List<double>>> cv in valuesByCL)
                    {
                        List<double> averages = new List<double>();
                        double sample1_avg = 0;
                        double sample2_avg = 0;
                        double sample3_avg = 0;

                        foreach (List<double> ls in cv.Value)
                        {
                            if (ls.Count() > 0) { sample1_avg += ls[0]; }
                            if (ls.Count() > 1) { sample2_avg += ls[1]; }
                            if (ls.Count() > 2) { sample3_avg += ls[2]; }
                        }
                        
                        averages.Add(sample1_avg);
                        averages.Add(sample2_avg);
                        averages.Add(sample3_avg);

                        averagesByCL.Add(cv.Key, averages);
                    }

                    int index = 2;

                    foreach (KeyValuePair<string, List<double>> cv in averagesByCL)
                    {
                        worksheet.Cells[a_row + 1, col + index] = new ExcelLibrary.SpreadSheet.Cell(cv.Key, "Bold");
                        worksheet.Cells[s_row + 1, col + index] = new ExcelLibrary.SpreadSheet.Cell(cv.Key, "Bold");

                        worksheet.Cells[a_row2, col + index] = new ExcelLibrary.SpreadSheet.Cell(CalculateAverage(cv.Value));
                        worksheet.Cells[s_row2, col + index] = new ExcelLibrary.SpreadSheet.Cell(CalculateStdDev(cv.Value));

                        index++;
                    }

                    a_row2++;
                    s_row2++;
                }

                worksheet.Cells[a_row, col] = new ExcelLibrary.SpreadSheet.Cell("Group " + runsByGroup.Key + ": AVERAGE");
                worksheet.Cells[s_row, col] = new ExcelLibrary.SpreadSheet.Cell("Group " + runsByGroup.Key + ": STDEV");

                a_row++;
                s_row++;

                worksheet.Cells[a_row, col] = new ExcelLibrary.SpreadSheet.Cell("Date");
                worksheet.Cells[a_row, col + 1] = new ExcelLibrary.SpreadSheet.Cell("Time");
                worksheet.Cells[s_row, col] = new ExcelLibrary.SpreadSheet.Cell("Date");
                worksheet.Cells[s_row, col + 1] = new ExcelLibrary.SpreadSheet.Cell("Time");

                a_row = s_row2 + 2;
                s_row = a_row + 3 + runs.ElementAt(0).Value.Count();
            }

            return worksheet;
        }

        private double CalculateAverage(IEnumerable<double> values)
        {
            double ret = 0;
            int count = 0;

            if (values.Count() > 0)
            {
                foreach (double v in values)
                {
                    if (v != 0) 
                    { 
                        ret += v;
                        count++;
                    }
                }

                if (count != 0) { ret = ret / count; }
            }
            return ret;
        }

        private double CalculateStdDev(IEnumerable<double> values)
        {
            double ret = 0;
            if (values.Count() > 0)
            {
                //Compute the Average      
                double avg = values.Average();
                //Perform the Sum of (value-avg)_2_2      
                double sum = values.Sum(d => Math.Pow(d - avg, 2));
                //Put it all together      
                ret = Math.Sqrt((sum) / (values.Count() - 1));
            }
            return ret;
        }

        public Dictionary<double, List<Peak>> GetPeaksForCalibration(List<CaliConc> cali)
        {
            Dictionary<double, List<Peak>> caliPeaks = new Dictionary<double, List<Peak>>();
            foreach (CaliConc cc in cali)
            {
                int runID = cc.RunID;

                var peaks = from x in db.Peaks
                       where x.RunID == runID
                       select x;

                caliPeaks.Add(cc.Conc, peaks.ToList());
            }

            return caliPeaks;
        }

        public List<Calibration> GetCalibrationResults(List<CaliPoint> caliCurve)
        {
            Dictionary<string, List<CaliPoint>> caliPoints = new Dictionary<string, List<CaliPoint>>();

            List<CaliPoint> caliPointList = new List<CaliPoint>();

            foreach (CaliPoint cps in caliCurve)
            {
                CaliPoint cp = new CaliPoint();

                cp.Compound = cps.Compound;
                cp.Time = cps.Time;
                cp.RunID = cps.RunID;
                cp.Conc = cps.Conc;

                var peaks = from x in db.Peaks
                            where x.RunID == cp.RunID && x.Time == cp.Time
                            select x;

                cp.PeakID = peaks.ToList().ElementAt(0).PeakID;
                cp.Area = peaks.ToList().ElementAt(0).Area;
                cp.SequenceName = peaks.ToList().ElementAt(0).SequenceName;
                cp.SequenceID = peaks.ToList().ElementAt(0).SequenceID;

                caliPointList.Add(cp);
            }

            foreach (CaliPoint cp in caliPointList)
            {
                if(!caliPoints.ContainsKey(cp.Compound)) 
                {
                    caliPoints.Add(cp.Compound, new List<CaliPoint>());
                } 
                
                caliPoints[cp.Compound].Add(cp);
            }

            return GenerateCaliCurve(caliPoints);
        }

        private List<Calibration> GenerateCaliCurve(Dictionary<string, List<CaliPoint>> caliPoints)
        {
            List<Calibration> caliList = new List<Calibration>();

            foreach (KeyValuePair<string, List<CaliPoint>> kvp in caliPoints)
            {
                double[] x = new double[caliPoints.Count];
                double[] y = new double[caliPoints.Count];

                int count = 0;
                foreach (CaliPoint cp in kvp.Value)
                {
                    y[count] = cp.Area;
                    x[count] = cp.Conc;
                    count++;
                }

                object[,] result = Linest(x, y);

                Calibration calibration = new Calibration();
                
                calibration.Compound        = kvp.Key;
                calibration.SequenceName    = kvp.Value.ElementAt(0).SequenceName;
                calibration.SequenceID      = kvp.Value.ElementAt(0).SequenceID;
                calibration.Time            = kvp.Value.ElementAt(0).Time;
                calibration.Slope           = Convert.ToDouble(result[1, 1]);
                calibration.Intercept       = Convert.ToDouble(result[1, 2]);
                calibration.SlopePM         = Convert.ToDouble(result[2, 1]);
                calibration.InterceptPM     = Convert.ToDouble(result[2, 2]);
                calibration.RSQ             = Convert.ToDouble(result[3, 1]);
                calibration.STEYX           = Convert.ToDouble(result[3, 2]);
                calibration.FStat           = Convert.ToDouble(result[4, 1]);
                calibration.DegreeFreedom   = Convert.ToDouble(result[4, 2]);
                calibration.RegSumSquares   = Convert.ToDouble(result[5, 1]);
                calibration.ResidualSS      = Convert.ToDouble(result[5, 2]);

                caliList.Add(calibration);
            }

            return caliList;
        }

        private object[,] Linest(double[] x, double[] y)
        {
            string name = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

            Microsoft.Office.Interop.Excel.Application xl = new Microsoft.Office.Interop.Excel.Application();
            Microsoft.Office.Interop.Excel.WorksheetFunction wsf = xl.WorksheetFunction;
            object[,] result = (object[,])wsf.LinEst(y, x, false, true);

            return result;
        }
    }

    public class Sequence
    {
        public int SequenceID { get; set; }
        public string SequenceName { get; set; }
        public int NumberOfRuns{ get; set; }
        public DateTime Uploaded { get; set; }
        public DateTime ExperimentDate { get; set; }
    }

    public class Run
    {
        public int RunID { get; set; }
        public int SequenceID { get; set; }
        public string SequenceName { get; set; }
        public string RunName { get; set; }
        public double Dilution { get; set; }
        public double CDW { get; set; }
        public double SampleVolume { get; set; }
        public double CDWVolume { get; set; }
        public double BMCmmol { get; set; }
        public double CellCount { get; set; }
    }

    public class Peak
    {
        public int PeakID { get; set; }
        public int RunID { get; set; }
        public int SequenceID { get; set; }
        public string SequenceName { get; set; }
        public string RunName { get; set; }
        public double Time { get; set; }
        public double Area { get; set; }
        public double Height { get; set; }
        public double Width { get; set; }
        public double Concentration { get; set; }
    }

    public class CaliConc
    {
        public int RunID { get; set; }
        public double Conc { get; set; }
    }

    public class CaliPoint
    {
        public string Compound { get; set; }
        public double Time { get; set; }
        public int PeakID { get; set; }
        public int RunID { get; set; }
        public int SequenceID { get; set; }
        public string SequenceName { get; set; }
        public double Area { get; set; }
        public double Conc { get; set; }
    }

    public class Calibration
    {
        public int CalibrationID { get; set; }
        public string CalibrationName { get; set; }
        public int SequenceID { get; set; }
        public string SequenceName { get; set; }
        public string Compound { get; set; }
        public double Slope { get; set; }
        public double Intercept { get; set; }
        public double SlopePM { get; set; }
        public double InterceptPM { get; set; }
        public double RSQ { get; set; }
        public double STEYX { get; set; }
        public double FStat { get; set; }
        public double DegreeFreedom { get; set; }
        public double RegSumSquares { get; set; }
        public double ResidualSS { get; set; }
        public DateTime DateCreated { get; set; }
        public int Current { get; set; }
        public double Time { get; set; } 
    }

    public class QuantificationRange
    {
        [Key] public int RangeID { get; set; }
        public string RangeName { get; set; }
        public double FFA_Start { get; set; }
        public double FFA_End { get; set; }
        public double MAG_Start { get; set; }
        public double MAG_End { get; set; }
        public double DAG_Start { get; set; }
        public double DAG_End { get; set; }
        public double TAG_Start { get; set; }
        public double TAG_End { get; set; }
    }

    public class Quantification
    {
        [Key] public int QuantificationID { get; set; }
        public int RangeID { get; set; }
        public string RangeName { get; set; }
        public string Compound { get; set; }
        public double RT_Start { get; set; }
        public double RT_End { get; set; }
    }

    public class RunQuantification
    {
        public int RangeID { get; set; }
        public int RunID { get; set; }
        public string RunName { get; set; }
        public DateTime ExperimentDate { get; set; }
        public string CalibrationName { get; set; }
        public double DilutionFactor { get; set; }
        public double BMWeight { get; set; }
        public double BMWeightCmmol { get; set; }
        public double SampleVolume { get; set; }
        public double BMVolume { get; set; }
        public long CellCount { get; set; }
        public int Group { get; set; }
        public List<CompoundData> CompData { get; set; }
        public TotalData TotData { get; set; }
    }

    public class CompoundData
    {
        public string Compound { get; set; }
        public string Label { get; set; }
        public double Conc_mgmL { get; set; }
        public double Conc_sv { get; set; }
        public double Conc_ww { get; set; }
        public double Conc_cmmol { get; set; }
        public double Conc_1000cells { get; set; }
        public double PercentTotal { get; set; }
    }

    public class TotalData
    {
        public double TotalFFA_mgmL { get; set; }
        public double TotalFFA_sv { get; set; }
        public double TotalFFA_ww { get; set; }
        public double TotalFFA_cmmol { get; set; }
        public double TotalFFA_1000cells { get; set; }

        public double TotalMAG_mgmL { get; set; }
        public double TotalMAG_sv { get; set; }
        public double TotalMAG_ww { get; set; }
        public double TotalMAG_cmmol { get; set; }
        public double TotalMAG_1000cells { get; set; }

        public double TotalDAG_mgmL { get; set; }
        public double TotalDAG_sv { get; set; }
        public double TotalDAG_ww { get; set; }
        public double TotalDAG_cmmol { get; set; }
        public double TotalDAG_1000cells { get; set; }

        public double TotalTAG_mgmL { get; set; }
        public double TotalTAG_sv { get; set; }
        public double TotalTAG_ww { get; set; }
        public double TotalTAG_cmmol { get; set; }
        public double TotalTAG_1000cells { get; set; }

        public double TotalLipid_mgmL { get; set; }
        public double TotalLipid_sv { get; set; }
        public double TotalLipid_ww { get; set; }
        public double TotalLipid_cmmol { get; set; }
        public double TotalLipid_1000cells { get; set; }
    }

    public class StdBeta
    {
        public string Label { get; set; }
        public double MW { get; set; }
        public int NumC { get; set; }
    }

    public class DUMP
    {
        public List<Run> runList { get; set; }
        public List<QuantificationRange> rangeList { get; set; }
        public List<Calibration> caliList { get; set; }
    }

    public class GC_DBContext : DbContext
    {
        public DbSet<Sequence> Sequences { get; set; }
        public DbSet<Run> Runs { get; set; }
        public DbSet<Peak> Peaks { get; set; }
        public DbSet<Calibration> Calibrations { get; set; }
        public DbSet<QuantificationRange> QuantificationRanges { get; set; }
        public DbSet<Quantification> Quantifications { get; set; }
    }

}