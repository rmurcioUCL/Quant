using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Diagnostics; //debugging - stopwatch
using System.Windows.Forms;
using System.IO;

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
        public string OutTPredCombined = "TObs.bin";
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

        public static int NumModes = 3;

        public FMatrix[] TObs = new FMatrix[NumModes]; //Data input to model.
        public FMatrix[] dis = new FMatrix[NumModes]; //distance matrix for zones in TObs
        public List<List<int>> positions = new List<List<int>>();
        public FMatrix[] TObsCopy = new FMatrix[NumModes]; //Data input to model.
        //public int N; //Number of zones i.e. used for dimensioning all arrays

        public bool IsUsingConstraints = false;
        public float[] Constraints; //1 or 0 to indicate constraints for zones matching TObs - this applies to all modes

        public FMatrix[] TPred = new FMatrix[NumModes]; //this is the output


        public float[] B; //this is the constraints output vector - this applies to all modes
        public float[] Beta; //Beta values for three modes - this is also output
        public int nexp;
        private int modelType;

        public QUANT3Model(int modelType)
        {
            this.modelType = modelType;
        }

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
        public FMatrix[] LoadAndRun(QUANT3ModelProperties q3mp, FolderBrowserDialog f, ProgressBar pb,int exps)
        {

            nexp = exps;
            switch (this.modelType)
            {
                case 3:
                    NumModes = 2;break;
                default:
                    NumModes = 3; break;
            }

            for (int k = 0; k < NumModes; k++)
            {
                dis[k] = FMatrix.DirtyDeserialise(f.SelectedPath+"\\"+q3mp.Indis[k]);
                TObs[k] = FMatrix.DirtyDeserialise(f.SelectedPath+ "\\" + q3mp.InTObs[k]);
                TObsCopy[k] = FMatrix.DirtyDeserialise(f.SelectedPath + "\\" + q3mp.InTObs[k]);
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
            Run(pb, f.SelectedPath);
            pb.PerformStep();

            //constraints B weights if we're doing that
            if (IsUsingConstraints) Serialiser.Put(q3mp.OutConstraintsB, B);
            System.Diagnostics.Debug.WriteLine("DONE");
            pb.Refresh();
            return TPred;

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
        public void Run(ProgressBar pb, String SelectedPath)
        {
            int N = TObs[0].M; //hopefully [0] and [1] and [2] are the same
            if (nexp > 1) { 
                float total = 345214.0f;
                float target = 0.9f * total;
                float rndvaluet = 0.0f;
                Random rnd = new Random();

                ///
                int col = 1;
                int row = 1;

                while (rndvaluet <= target)
                {
                    col = rnd.Next(0, N);
                    row = rnd.Next(0, N);

                    if (TObs[0]._M[col, row] != 0 || TObs[1]._M[col, row] != 0 || TObs[2]._M[col, row] != 0)
                    { // countZero += 1; 
                        TObs[0]._M[col, row] = 0.0f;
                        TObs[1]._M[col, row] = 0.0f;
                        TObs[2]._M[col, row] = 0.0f;
                        positions.Add(new List<int> { col, row });
                        rndvaluet += 1;
                    }

                }
            }
 
            //set up Beta for modes 0, 1 and 2 to 1.0f
            Beta = new float[NumModes];
            for (int k = 0; k < NumModes; k++) Beta[k] = 1.0f;

            //work out Dobs and Tobs from rows and columns of TObs matrix
            //These don't ever change so they need to be outside the convergence loop
            float[] DjObs = new float[N];
            float[] OiObs = new float[N];
            float Sum;
            // System.Diagnostics.Debug.WriteLine("End while " + rndvaluet);
            ////


            //OiObs
            for (int i = 0; i < N; i++)
            {
                Sum = 0;
                for (int j = 0; j < N; j++)
                {
                    for (int k = 0; k < NumModes; k++) Sum += TObs[k]._M[i, j];
                }

                OiObs[i] = Sum;
                // System.Diagnostics.Debug.WriteLine(OiObs[i]);
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

                bool ConstraintsMet = false;
                do
                {
                    //residential constraints
                    ConstraintsMet = true; //unless violated one or more times below
                    int FailedConstraintsCount = 0;

                    //model run
                    for (int k = 0; k < NumModes; k++) //mode loop
                    {
                        //Parallel.For(0, N, i =>
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
                                Tij[k]._M[i, j] = (float)(B[j] * OiObs[i] * DjObs[j] * Math.Exp(-Beta[k] * dis[k]._M[i, j]) / denom);
                            }
                        }
                        //);
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
                                    System.Diagnostics.Debug.WriteLine("Dj=" + Dj + " Zj=" + Z[j] + " Bj=" + B[j]);
                                }
                            }
                        }

                    }


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
                //System.Diagnostics.Debug.WriteLine("count = " + countR);

                for (int k = 0; k < NumModes; k++)
                {
                    System.Diagnostics.Debug.WriteLine("k=" + k + " - value= " + delta[k] / CBarObs[k]);
                    //if (delta[k] / CBarObs[k] > factor)
                    if (delta[k] / CBarObs[k] > 0.001)
                    //if (delta[k] / CBarObs[k] > 0.52)
                    {
                        Beta[k] = Beta[k] * CBarPred[k] / CBarObs[k];
                        System.Diagnostics.Debug.WriteLine("Beta " + Beta[k]);
                        Converged = false;
                    }

                }

                float deltat = 0.0f;
                for (int k = 0; k < NumModes; k++)
                {
                    deltat += delta[k];
                }

            }

            //Set the output, TPred[]
            for (int k = 0; k < NumModes; k++) TPred[k] = Tij[k];



            if (nexp > 1) {
                //save Betas
                if (!File.Exists(SelectedPath + "\\" + "betas.csv"))
                {
                    using (StreamWriter r = File.CreateText(SelectedPath + "\\" + "betas.csv")) r.Write(Beta[0] + "," + Beta[1] + "," + Beta[2] + "\n");
                }
                else
                {
                    using (StreamWriter r = File.AppendText(SelectedPath + "\\" + "betas.csv")) r.Write(Beta[0] + "," + Beta[1] + "," + Beta[2] + "\n");
                }
                //RMSE
                /*FMatrix TPredCombinedp = new FMatrix(N, N);
                FMatrix TObsCombined = new FMatrix(N, N);
                float Sump = 0.0f;
                float Sumo=0.0f;
                for (int w = 0; w < TPredCombinedp.M; w++)
                {
                    for (int j = 0; j < TPredCombinedp.N; j++)
                    {
                    
                        for (int k = 0; k < 3; k++)
                        {
                            Sump += TPred[k]._M[w, j];
                            Sumo += TObsCopy[k]._M[w, j];
                        }
                        TPredCombinedp._M[w, j] = Sump;
                        TObsCombined._M[w, j] = Sumo;
                        Sump = 0.0f;
                        Sumo = 0.0f;
                    }
                }*/


                double[] sumT = { 0.0f, 0.0f, 0.0f };
                double[] difsq = { 0.0f, 0.0f, 0.0f };
                double[] sumV = { 0.0f, 0.0f, 0.0f };
                foreach (List<int> subList in positions)
                {
                    for (int k = 0; k < NumModes; k++) {
                        difsq[k] = (TPred[k]._M[subList[0], subList[1]] - TObsCopy[k]._M[subList[0], subList[1]]) * (TPred[k]._M[subList[0], subList[1]] - TObsCopy[k]._M[subList[0], subList[1]]);
                        sumT[k] = sumT[k] + difsq[k];
                        sumV[k] = sumV[k] + TPred[k]._M[subList[0], subList[1]];
                    }
                    if (!File.Exists(SelectedPath + "\\" + "removed_" + nexp + ".csv"))
                    {
                        using (StreamWriter f = File.CreateText(SelectedPath + "\\" + "removed_" + nexp + ".csv")) f.Write(subList[0] + "," + subList[1] + "\n");
                    }
                    else
                    {
                        using (StreamWriter f = File.AppendText(SelectedPath + "\\" + "removed_" + nexp + ".csv")) f.Write(subList[0] + "," + subList[1] + "\n");
                    }
                }
                double sumAllV = sumV[0] + sumV[1] + sumV[2];
                double avg =  sumAllV / (3*positions.Count);
                double[] sumOfSquares = { 0.0f, 0.0f, 0.0f };
                foreach (List<int> subList in positions) {
                    for (int k = 0; k < NumModes; k++)
                    {
                        sumOfSquares[k] += Math.Pow((TPred[k]._M[subList[0], subList[1]] - avg), 2.0);
                    }
                }
                double sumOfSquaresT = sumOfSquares[0] + sumOfSquares[1] + sumOfSquares[2];
                double sigma = sumOfSquaresT / (3*(positions.Count - 1));
                double sd = Math.Sqrt(sigma);
                double sumAllT = sumT[0] + sumT[1] + sumT[2];
                double rmse =  Math.Sqrt(sumAllT / (3*positions.Count));
                if (!File.Exists(SelectedPath + "\\" + "rmse.csv")) {
                    using (StreamWriter sw = File.CreateText(SelectedPath + "\\" + "rmse.csv")) sw.Write(rmse+","+sigma+","+sd+"\n");
                }
                else { 
                    using (StreamWriter sw = File.AppendText(SelectedPath + "\\" + "rmse.csv")) sw.Write(rmse+","+sigma+","+sd + "\n");
                }
                //END RMSE
            }
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