using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Diagnostics; //debugging - stopwatch
using System.Windows.Forms;
using System.IO;
using System.Linq;

namespace QUANT
{
    /// <summary>
    /// Enumeration of Quant 2 modal numbers for all array indexes (TObs[Q3Road], TPred[Q3Road], Beta[Q3Road] etc).
    /// </summary>
    public enum QUANT3Modes { Q3Road = 0, Q3Bus = 1, Q3Rail = 2 };
    
    /// <summary>
    /// Data class used to pass data in a parameter to the LoadAndRun method. Used with the ModelRunHub and HangFire scheduler object.
    /// Note the use of 1,2,3 rather than 0,1,2 for the mode.
    /// </summary>
    public class QUANT3ModelProperties
    {
        public bool IsUsingConstraints = false;
        public string[] InTObs = { "TObs_1.bin", "TObs_2.bin", "TObs_3.bin" };
        public string[] Indis = { "dis_roads_min.bin", "dis_bus_min.bin", "dis_gbrail_min.bin" };
        public string InConstraints = "";
        public string InPopulationFilename = "";

        public string InOutStatisticsFilename = ""; //in or out depending on method

        public string[] OutTPred = { "TPred_1.bin", "TPred_2.bin", "TPred_3.bin" };
        public string OutTPredCombined = "TPred.bin";
        public string OutConstraintsB = "";

    }

    /// <summary>
    /// Three mode QUANT Model
    /// K=0=road seconds
    /// K=1=bus seconds
    /// K=2=rail seconds
    /// </summary>
    public class QUANT3Model
    {
        #region properties

        public const int NumModes = 3;

        public FMatrix[] TObs = new FMatrix[NumModes]; //Data input to model.
        public FMatrix[] dis = new FMatrix[NumModes]; //distance matrix for zones in TObs
        //public int N; //Number of zones i.e. used for dimensioning all arrays

        public bool IsUsingConstraints = false;
        public float[] Constraints; //1 or 0 to indicate constraints for zones matching TObs - this applies to all modes

        public FMatrix[] TPred = new FMatrix[NumModes]; //this is the output
        public float[] B; //this is the constraints output vector - this applies to all modes
        public float[] Beta; //Beta values for three modes - this is also output

        #endregion properties

        #region methods

        /// <summary>
        /// Mean trips calculation
        /// </summary>
        /// <param name="Tij"></param>
        /// <param name="dis"></param>
        /// <returns></returns>
        public float CalculateCBar(ref FMatrix Tij, ref FMatrix dis)
        {
            int N = Tij.N;
            float CNumerator = 0, CDenominator = 0;
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    CNumerator += Tij._M[i, j] * dis._M[i, j];
                    CDenominator += Tij._M[i, j];
                    //if (float.IsNaN(Tij._M[i, j]))
                    //{
                    //    System.Diagnostics.Debug.WriteLine("NaN " + i + " " + j);
                    //}
                    //if (float.IsNaN(dis._M[i, j]))
                    //{
                    //    System.Diagnostics.Debug.WriteLine("NaN dis " + i + " " + j);
                    //}
                    //if (dis._M[i, j] <= 0)
                    //{
                    //    System.Diagnostics.Debug.WriteLine("Zero dis " + i + " " + j);
                    //}
                }
            }
            float CBar = CNumerator / CDenominator;

            return CBar;
        }

        /// <summary>
        /// Initialise the model from the Quant2ModelProperties data and run it.
        /// </summary>
        /// <param name="ConnectionId"></param>
        /// <param name="q3mp"></param>
        public void LoadAndRun(QUANT3ModelProperties q3mp, FolderBrowserDialog f, ProgressBar pb)
        {
            // HubConnectionId = ConnectionId;
            //deserialise the distance and TObs matrices
            // InstrumentStatusText = "Loading matrices";
            /*string[] files = Directory.GetFiles(f.SelectedPath);
            System.Windows.Forms.MessageBox.Show("Files found: " + files.Length.ToString(), "Message");*/
            /*for (int i=0;i< files.Length;i++)
                System.Windows.Forms.MessageBox.Show("Files found: " + files.ElementAt(i), "Message");*/
            for (int k = 0; k < NumModes; k++)
            {
                dis[k] = FMatrix.DirtyDeserialise(f.SelectedPath+"\\"+q3mp.Indis[k]);
                TObs[k] = FMatrix.DirtyDeserialise(f.SelectedPath+ "\\" + q3mp.InTObs[k]);
            }

            ////IsUsingConstraints = q3mp.IsUsingConstraints;
            if (IsUsingConstraints)
            {
                //set up the constraints input data from the green belt and land use data table
                Constraints = new float[TObs[0].N];
                //load constraints file here - convert data table to array based on zonei code
                DataTable dt = (DataTable)Serialiser.Get(q3mp.InConstraints);
                int ConstraintCount = 0;
                foreach (DataRow row in dt.Rows) //this does assume that there is a zonei value for every slot in Constraints[]
                {
                    int ZoneI = (int)row["zonei"];
                    float Gj = (float)row["Gj"];
                    Constraints[ZoneI] = Gj;
                    if (Gj >= 1.0) ++ConstraintCount;
                }
                System.Diagnostics.Debug.WriteLine("ConstraintCount=" + ConstraintCount);
            }


            // InstrumentStatusText = "Starting model run";
            pb.PerformStep();
            Run(pb);
            pb.PerformStep();
            //write out 3 modes of predicted matrix
            for (int k = 0; k < NumModes; k++)
            {
                TPred[k].DirtySerialise(f.SelectedPath + "\\" + q3mp.OutTPred[k]);
            }

            //and a combined mode so the visualisation can see the data

            //TODO: this block needs to change once the GUI is able to visualise the different modes separately
            FMatrix TPredCombined = new FMatrix(TPred[0].M, TPred[0].N);
            for (int i = 0; i < TPredCombined.M; i++)
            {
                for (int j = 0; j < TPredCombined.N; j++)
                {
                    float Sum = 0;
                    for (int k = 0; k < NumModes; k++)
                    {
                        Sum += TPred[k]._M[i, j];
                    }
                    TPredCombined._M[i, j] = Sum;
                }
            }
            TPredCombined.DirtySerialise(f.SelectedPath + "\\" + q3mp.OutTPredCombined);
            //end combination block

            //constraints B weights if we're doing that
            if (IsUsingConstraints) Serialiser.Put(q3mp.OutConstraintsB, B);
            System.Diagnostics.Debug.WriteLine("DONE");
            /*InstrumentStatusText = "Computing model statistics";
            StatisticsDataQ3 sd = new StatisticsDataQ3();
            DataTable PopulationTable = (DataTable)Serialiser.Get(q3mp.InPopulationFilename);
            sd.ComputeFromModel(this, ref PopulationTable);
            sd.SerialiseToXML(q3mp.InOutStatisticsFilename);
            InstrumentStatusText = "Finished";
            InstrumentFinished();*/
        }

        private void TestMatrix(ref FMatrix M)
        {
            int N = M.N;
            int ZeroCount = 0, NaNCount = 0;
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    if (M._M[i, j] <= 0) ++ZeroCount;
                    if (float.IsNaN(M._M[i, j])) ++NaNCount;
                }
            }
            System.Diagnostics.Debug.WriteLine("ZeroCount=" + ZeroCount + " NaNCount=" + NaNCount);
        }

        private void TestVector(ref float[] V)
        {
            int ZeroCount = 0, NaNCount = 0;
            for (int i = 0; i < V.Length; i++)
            {
                if (V[i] <= 0) ++ZeroCount;
                if (float.IsNaN(V[i])) ++NaNCount;
            }
            System.Diagnostics.Debug.WriteLine("ZeroCount=" + ZeroCount + " NaNCount=" + NaNCount);
        }

        //this is runwithconstraints
        public void Run(ProgressBar pb)
        {
            int N = TObs[0].M; //hopefully [0] and [1] and [2] are the same

            //set up Beta for modes 0, 1 and 2 to 1.0f
            Beta = new float[NumModes];
            for (int k = 0; k < NumModes; k++) Beta[k] = 1.0f;

            //work out Dobs and Tobs from rows and columns of TObs matrix
            //These don't ever change so they need to be outside the convergence loop
            float[] DjObs = new float[N];
            float[] OiObs = new float[N];
            float Sum;

            //OiObs
            for (int i = 0; i < N; i++)
            {
                Sum = 0;
                for (int j = 0; j < N; j++)
                {
                    for (int k = 0; k < NumModes; k++) Sum += TObs[k]._M[i, j];
                }
                OiObs[i] = Sum;
            }

            //DjObs
            for (int j = 0; j < N; j++)
            {
                Sum = 0;
                for (int i = 0; i < N; i++)
                {
                    for (int k = 0; k < NumModes; k++) Sum += TObs[k]._M[i, j];
                }
                DjObs[j] = Sum;
            }

            //constraints initialisation
            B = new float[N];
            for (int i = 0; i < N; i++) B[i] = 1.0f; //hack
            float[] Z = new float[N];
            for (int j = 0; j < N; j++)
            {
                Z[j] = float.MaxValue;
                if (IsUsingConstraints)
                {
                    if (Constraints[j] >= 1.0f) //constraints array taking place of Gj (Green Belt) in documentation
                    {
                        //Gj=1 means 0.8 of MSOA land is green belt, so can't be built on
                        //set constraint to original Dj
                        Z[j] = DjObs[j];
                    }
                }
            }
            //end of constraints initialisation - have now set B[] and Z[] based on IsUsingConstraints, Constraints[] and DObs[]

            //Instrumentation block - mainly to start off the graph so you have something to look at while the next bit happens
            /*for (int k = 0; k < NumModes; k++)
                InstrumentSetVariable("Beta" + k, Beta[k]);
            InstrumentSetVariable("delta", 0);
            InstrumentTimeInterval();*/
            //end of Instrumentation block

            FMatrix[] Tij = null;
            bool Converged = false;
            int countR = 0;
            Tij = new FMatrix[NumModes];
            for (int k = 0; k < NumModes; k++)
            {
                Tij[k] = new FMatrix(N, N);
            }
            while (!Converged)
            {
                pb.PerformStep();
                //Instrumentation block
                /* for (int k = 0; k < NumModes; k++)
                 {
                     InstrumentSetVariable("Beta" + k, Beta[k]);
                     InstrumentSetVariable("delta" + k, 100);
                     InstrumentSetVariable("delta", 100);
                 }*/
                //end of instrumentation block


                bool ConstraintsMet = false;
               

                do
                {
                    //residential constraints
                    ConstraintsMet = true; //unless violated one or more times below
                    int FailedConstraintsCount = 0;

                    //model run
                    //Tij = new FMatrix[NumModes];
                    for (int k = 0; k < NumModes; k++) //mode loop
                    {
                        //InstrumentStatusText = "Running model for mode " + k;
                        //Tij[k] = new FMatrix(N, N);

                        Parallel.For(0, N, i =>
                        //for (int i = 0; i < N; i++)
                        {
                            //denominator calculation which is sum of all modes
                            double denom = 0;
                            for (int kk = 0; kk < NumModes; kk++) //second mode loop
                            {
                                for (int j = 0; j < N; j++)
                                {
                                    denom += DjObs[j] * Math.Exp(-Beta[kk] * dis[kk]._M[i, j]);
                                }
                            }

                            //numerator calculation for this mode (k)
                            for (int j = 0; j < N; j++)
                            {
                                Tij[k]._M[i, j] = (float)(B[j] * OiObs[i] * DjObs[j] * Math.Exp(-Beta[k] * dis[k]._M[i, j]) / denom);
                            }
                        }
                        );
                    }

                    //constraints check
                    if (IsUsingConstraints)
                    {
                        System.Diagnostics.Debug.WriteLine("Constraints test");
                       // InstrumentStatusText = "Constraints test";

                        for (int j = 0; j < N; j++)
                        {
                            float Dj = 0;
                            for (int i = 0; i < N; i++) Dj += Tij[0]._M[i, j] + Tij[1]._M[i, j] + Tij[2]._M[i, j];
                            if (Constraints[j] >= 1.0f) //Constraints is taking the place of Gj in the documentation
                            {
                                if ((Dj - Z[j]) >= 0.5) //was >1.0
                                {
                                    B[j] = B[j] * Z[j] / Dj;
                                    ConstraintsMet = false;
                                    ++FailedConstraintsCount;
                                   // InstrumentStatusText = "Constraints violated on " + FailedConstraintsCount + " MSOA zones";
                                    System.Diagnostics.Debug.WriteLine("Dj=" + Dj + " Zj=" + Z[j] + " Bj=" + B[j]);
                                }
                            }
                        }

                        //copy B2 into B ready for the next round
                        //for (int j = 0; j < N; j++) B[j] = B2[j];
                    }
                    //System.Diagnostics.Debug.WriteLine("FailedConstraintsCount=" + FailedConstraintsCount);

                    //Instrumentation block
                    //for (int k = 0; k < NumModes; k++)
                    //    InstrumentSetVariable("Beta" + k, Beta[k]);
                    //InstrumentSetVariable("delta", FailedConstraintsCount); //not technically delta, but I want to see it for testing
                    //InstrumentTimeInterval();
                    //end of instrumentation block

                } while (!ConstraintsMet);

                //calculate mean predicted trips and mean observed trips (this is CBar)
                float[] CBarPred = new float[NumModes];
                float[] CBarObs = new float[NumModes];
                float[] delta = new float[NumModes];
                for (int k = 0; k < NumModes; k++)
                {
                    CBarPred[k] = CalculateCBar(ref Tij[k], ref dis[k]);
                    CBarObs[k] = CalculateCBar(ref TObs[k], ref dis[k]);
                    delta[k] = Math.Abs(CBarPred[k] - CBarObs[k]); //the aim is to minimise delta[0]+delta[1]+...
                }

                //delta check on all betas (Beta0, Beta1, Beta2) stopping condition for convergence
                //double gradient descent search on Beta0 and Beta1 and Beta2
                Converged = true;
                countR++;
                System.Diagnostics.Debug.WriteLine("count = " + countR);
                for (int k = 0; k < NumModes; k++)
                {
                    System.Diagnostics.Debug.WriteLine("k=" + k + " - value= " + delta[k] / CBarObs[k]);
                    //if (delta[k] / CBarObs[k] > factor)
                    if (delta[k] / CBarObs[k] > 0.001)
                    {
                        Beta[k] = Beta[k] * CBarPred[k] / CBarObs[k];
                        Converged = false;
                    }

                }
                System.Diagnostics.Debug.WriteLine("Converged = " + Converged);
                //if (countR == 100)
                //Converged = true;
                //Instrumentation block
                /*  for (int k = 0; k < NumModes; k++)
                  {
                      InstrumentSetVariable("Beta" + k, Beta[k]);
                      InstrumentSetVariable("delta" + k, delta[k]);
                  }*/
                float deltat = 0.0f;
                for (int k = 0; k < NumModes; k++)
                {
                    deltat += delta[k];
                }
                //InstrumentSetVariable("delta", delta[] + delta[NumModes - 2] + delta[NumModes-1]); //should be a k loop
                //InstrumentSetVariable("delta", deltat);
                //InstrumentTimeInterval();
                //end of instrumentation block
            }

            //Set the output, TPred[]
            for (int k = 0; k < NumModes; k++) TPred[k] = Tij[k];

            //debugging:
            //for (int i = 0; i < N; i++)
            //    System.Diagnostics.Debug.WriteLine("Quant3Model::Run::ConstraintsB," + i + "," + B[i]);

        }

        /// <summary>
        /// NOTE: this was copied directly from the Quant 1 model
        /// Run the quant model with different values for the Oi and Dj zones.
        /// Uses a guid to store the user defined model run data
        /// PRE: needs TObs, dis and beta
        /// TODO: need to instrument this
        /// TODO: writes out one file, which is the sum of the three predicted matrices produced
        /// </summary>
        /// <param name="OiDjHash">Hashmap of zonei index and Oi, Dj values for that area. A value of -1 for Oi or Dj means no change.</param>
        /// <param name="hasConstraints">Run with random values added to the Dj values.</param>
        /// <param name="OutFilename">Filename of where to store the resulting matrix, probably includes the GUID of the user directory to store the results</param>
        public void RunWithChanges(Dictionary<int, float[]> OiDjHash, bool hasConstraints, string OutFilename)
        {
            Stopwatch timer = Stopwatch.StartNew();

            int N = TObs[0].N;

            float[] DjObs = new float[N];
            float[] OiObs = new float[N];
            float Sum;

            //OiObs
            for (int i = 0; i < N; i++)
            {
                Sum = 0;
                for (int j = 0; j < N; j++)
                {
                    for (int k = 0; k < NumModes; k++) Sum += TObs[k]._M[i, j];
                }
                OiObs[i] = Sum;
            }

            //DjObs
            for (int j = 0; j < N; j++)
            {
                Sum = 0;
                for (int i = 0; i < N; i++)
                {
                    for (int k = 0; k < NumModes; k++) Sum += TObs[k]._M[i, j];
                }
                DjObs[j] = Sum;
            }

            //this is a complete hack - generate a TPred matrix that we can get Dj constraints from
            FMatrix[] TPredCons = new FMatrix[NumModes];
            for (int k = 0; k < NumModes; k++) //mode loop
            {
                TPredCons[k] = new FMatrix(N, N);

                for (int i = 0; i < N; i++)
                {
                    //denominator calculation which is sum of all modes
                    double denom = 0;
                    for (int kk = 0; kk < NumModes; kk++) //second mode loop
                    {
                        for (int j = 0; j < N; j++)
                        {
                            denom += DjObs[j] * Math.Exp(-Beta[kk] * dis[kk]._M[i, j]);
                        }
                    }

                    //numerator calculation for this mode (k)
                    for (int j = 0; j < N; j++)
                    {
                        TPredCons[k]._M[i, j] = (float)(B[j] * OiObs[i] * DjObs[j] * Math.Exp(-Beta[k] * dis[k]._M[i, j]) / denom);
                    }
                }
            }
            //now the DjCons - you could just set Zj here?
            float[] DjCons = new float[N];
            for (int j = 0; j < N; j++)
            {
                Sum = 0;
                for (int i = 0; i < N; i++)
                {
                    for (int k = 0; k < NumModes; k++) Sum += TPredCons[k]._M[i, j];
                }
                DjCons[j] = Sum;
            }


            //
            //
            //TODO: Question - do the constraints take place before or after the Oi Dj changes? If before, then it's impossible to increase jobs in greenbelt zones. If after, then changes override the green belt.

            //constraints initialisation - this is the same as the calibration, except that the B[j] values are initially taken from the calibration, while Z[j] is initialised from Dj[j] as before.
            float[] Z = new float[N];
            for (int j = 0; j < N; j++)
            {
                Z[j] = float.MaxValue;
                if (IsUsingConstraints)
                {
                    if (Constraints[j] >= 1.0f) //constraints array taking place of Gj (Green Belt) in documentation
                    {
                        //Gj=1 means a high enough percentage of MSOA land is green belt, so can't be built on
                        //set constraint to original Dj
                        //Z[j] = DjObs[j];
                        Z[j] = DjCons[j];
                    }
                }
            }
            //end of constraints initialisation - have now set B[] and Z[] based on IsUsingConstraints, Constraints[] and DObs[]

            //apply changes here from the hashmap
            foreach (KeyValuePair<int, float[]> KVP in OiDjHash)
            {
                int i = KVP.Key;
                if (KVP.Value[0] >= 0) OiObs[i] = KVP.Value[0];
                if (KVP.Value[1] >= 0) DjObs[i] = KVP.Value[1];
            }


            bool ConstraintsMet = false;
            do
            {
                //residential constraints
                ConstraintsMet = true; //unless violated one or more times below
                int FailedConstraintsCount = 0;

                //run 3 model
                System.Diagnostics.Debug.WriteLine("Run 3 model");
                for (int k = 0; k < NumModes; k++) //mode loop
                {
                    TPred[k] = new FMatrix(N, N);

                    Parallel.For(0, N, i =>
                    //for (int i = 0; i < N; i++)
                    {
                        //denominator calculation which is sum of all modes
                        double denom = 0;
                        for (int kk = 0; kk < NumModes; kk++) //second mode loop
                        {
                            for (int j = 0; j < N; j++)
                            {
                                denom += B[j] * DjObs[j] * Math.Exp(-Beta[kk] * dis[kk]._M[i, j]);
                            }
                        }

                        //numerator calculation for this mode (k)
                        for (int j = 0; j < N; j++)
                        {
                            TPred[k]._M[i, j] = (float)(B[j] * OiObs[i] * DjObs[j] * Math.Exp(-Beta[k] * dis[k]._M[i, j]) / denom);
                        }
                    }
                    );
                }

                //constraints check
                if (IsUsingConstraints)
                {
                    System.Diagnostics.Debug.WriteLine("Constraints test");

                    for (int j = 0; j < N; j++)
                    {
                        float Dj = 0;
                        for (int i = 0; i < N; i++) Dj += TPred[0]._M[i, j] + TPred[1]._M[i, j] + TPred[2]._M[i, j];
                        if (Constraints[j] >= 1.0f) //Constraints is taking the place of Gj in the documentation
                        {
                            //System.Diagnostics.Debug.WriteLine("Test: " + Dj + ", " + Z[j] + "," + B[j]);
                            if ((Dj - Z[j]) >= 0.5) //was >1.0 threshold
                            {
                                B[j] = B[j] * Z[j] / Dj;
                                ConstraintsMet = false;
                                ++FailedConstraintsCount;
                                //                                System.Diagnostics.Debug.WriteLine("Constraints violated on " + FailedConstraintsCount + " MSOA zones");
                                //                                System.Diagnostics.Debug.WriteLine("Dj=" + Dj + " Zj=" + Z[j] + " Bj=" + B[j]);
                            }
                        }
                    }
                }
            } while (!ConstraintsMet);

            //add all three TPred together
            FMatrix TPredAll = new FMatrix(N, N);
            //Parallel.For(0, N, i =>
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    Sum = 0;
                    for (int k = 0; k < NumModes; k++)
                    {
                        Sum += TPred[k]._M[i, j];
                    }
                    TPredAll._M[i, j] = Sum;
                }
            }
            //);

            //and store the result somewhere
            TPredAll.DirtySerialise(OutFilename);

            System.Diagnostics.Debug.WriteLine("QUANT3Model::RunWithChanges: " + timer.ElapsedMilliseconds + " ms");
        }

        #endregion methods
    }
}