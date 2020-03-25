using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Diagnostics;

//using OpenCL.Net.Extensions;
//using OpenCL.Net;

namespace QUANT
{
    //OK, it's a Fast Matrix (I hope)
    //TODO: need matrix base class with a sparse matrix sub class and probably a full matrix sub class as well
    //Have a look at this for OpenCL MatrixMul: http://www.cmsoft.com.br/opencl-tutorial/case-study-matrix-multiplication/
    [Serializable]
    public class FMatrix
    {
        public int N,M;
        public float[,] _M;

        #region properties

        #endregion properties

        #region methods
        /// <summary>
        /// Create a new M x N matrix (Amn where M are rows and N are columns).
        /// This is ROW MAJOR ordering.
        /// </summary>
        /// <param name="M">Rows</param>
        /// <param name="N">Columns</param>
        public FMatrix(int M, int N)
        {
            this.M = M;
            this.N = N;
            //System.Diagnostics.Debug.WriteLine(M);
            _M = new float[M, N];
        }

        /// <summary>
        /// Put some random values in to the matrix
        /// </summary>
        public void Randomise() {
            Random R = new Random();
            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    _M[i, j] = (float)R.NextDouble() * 100.0f; //random
                    //_M[i, j] = j + (i * (N + 1)); //assign the index number for testing (0,1,2,3...)
                }
            }
        }

        public void Init()
        {
            
            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    _M[i, j] = 0.0f; //random
                    //_M[i, j] = j + (i * (N + 1)); //assign the index number for testing (0,1,2,3...)
                }
            }
        }
        /// <summary>
        /// Return a scaled version of the current matrix
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public FMatrix Scale(float S) {
            FMatrix A = new FMatrix(M,N);
            for (int i=0; i<M; i++) {
                for (int j=0; j<N; j++) {
                    A._M[i, j] = S * this._M[i, j];
                }
            }
            return A;
        }
        public FMatrix Scale1(float S)
        {
            FMatrix A = new FMatrix(M, N);
            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    A._M[i, j] =  1 / this._M[i, j];
                }
            }
            return A;
        }
        public void PrintMatrix()
        {
            for (int i = 0; i < M; i++)
            {
                System.Diagnostics.Debug.Write("[ ");
                for (int j = 0; j < N; j++)
                {
                    System.Diagnostics.Debug.Write(_M[i, j] + " ");
                }
                System.Diagnostics.Debug.WriteLine("]");
            }
        }

        public FMatrix SumMatrix (FMatrix f1,int N)
        {
            FMatrix A = new FMatrix(M, N);
            
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    A._M[i, j] = this._M[i, j] + f1._M[i,j];
                }
            }
            return A;
        }

        /// <summary>
        /// Load an FMatrix from disk
        /// </summary>
        /// <param name="Filename"></param>
        /// <returns></returns>
        public static FMatrix Deserialise(string Filename)
        {
            FMatrix Mat=null;
            using (Stream stream = File.OpenRead(Filename))
            {
                BinaryFormatter deserializer = new BinaryFormatter();
                Mat = (FMatrix)deserializer.Deserialize(stream);
            }
            return Mat;
        }

        internal static FMatrix GenerateTripsLondon(string CSVFilename, int N)
        {
            //int N = 983;
            FMatrix TijObs = new FMatrix(N, N);
            string Line = "";
            using (StreamReader reader = new StreamReader(CSVFilename))
            {
                //string Line = reader.ReadLine(); //header line;

                string FLine = reader.ReadLine(); //header line
                int[] ColI = new int[N];
                string[] Fields = null;
                int LineCount = 0;
                while ((Line = reader.ReadLine()) != null)
                {
                    Fields = Line.Split(new char[] { ',' });
                    for (int i = 1; i < ColI.Length + 1; i++)
                    {
                        //TijObs._M[LineCount, i] = Int32.Parse(Fields[i]);
                        TijObs._M[LineCount, i - 1] = float.Parse(Fields[i]);
                    }
                    LineCount++;
                }
            }
            return TijObs;
        }

        /// <summary>
        /// Save an FMatrix to disk
        /// </summary>
        /// <param name="Filename"></param>
        public void Serialise(string Filename)
        {
            using (Stream stream = File.Create(Filename))
            {
                BinaryFormatter serializer = new BinaryFormatter();
                serializer.Serialize(stream,this);
            }
        }

        /// <summary>
        /// Write data in native binary format one row of data at a time. Each value occupies 4 bytes contiguously for the whole matrix.
        /// </summary>
        /// <param name="Filename"></param>
        //public void DirtySerialise(string Filename)
        //{
        //    byte[] buf = new byte[(N + 1) * sizeof(float)];
        //    using (BinaryWriter bw = new BinaryWriter(new MemoryStream(buf)))
        //    {
        //        using (Stream stream = File.Create(Filename))
        //        {
        //            for (int j = 0; j <= N; j++)
        //            {
        //                bw.Seek(0, SeekOrigin.Begin);
        //                for (int i = 0; i <= N; i++)
        //                {
        //                    bw.Write(_M[i, j]);
        //                }
        //                stream.Write(buf, 0, buf.Length);
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// Dirty serialise using a raw binary writer
        /// </summary>
        /// <param name="Filename"></param>
        //public void DirtySerialise(string Filename)
        //{
        //    using (BinaryWriter writer = new BinaryWriter(File.Create(Filename)))
        //    {
        //        for (int j = 0; j <= N; j++)
        //        {
        //            //bw.Seek(0, SeekOrigin.Begin);
        //            for (int i = 0; i <= N; i++)
        //            {
        //                writer.Write(_M[i, j]);
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// Serialise a matrix to a file using Buffer.BlockCopy to move big chunks of memory around FAST.
        /// The disk format is to write 4 bytes LSB first containing the matrix columns (N), followed by rows (M).
        /// Then the following bytes are 4 bytes for float format (single) of each array item in turn.
        /// The best yet - 15ms down from about 30s!
        /// DirtySerialise and DirtyDeserialise are opposite functions.
        /// Uses Row Major order i.e. row 1, followed by row 2 etc
        /// </summary>
        /// <param name="Filename"></param>
        public void DirtySerialise(string Filename)
        {
            //int s = sizeof(float);
            //System.Diagnostics.Debug.WriteLine("sizeof(float)=" + s);
            byte[] buf = new byte[N*sizeof(float)];
            //System.Diagnostics.Debug.WriteLine("buf.length=" + buf.Length);
            using (Stream stream = File.Create(Filename))
            {
                //not exactly elegant, but write little endian version of M and N to stream
                stream.WriteByte((byte)(M & 0xff));
                stream.WriteByte((byte)((M & 0xff00)>>8));
                stream.WriteByte((byte)((M & 0xff0000) >> 16));
                stream.WriteByte((byte)((M & 0xff000000) >> 24));
                stream.WriteByte((byte)(N & 0xff));
                stream.WriteByte((byte)((N & 0xff00) >> 8));
                stream.WriteByte((byte)((N & 0xff0000) >> 16));
                stream.WriteByte((byte)((N & 0xff000000) >> 24));
                for (int i = 0; i < M; i++)
                {
                    Buffer.BlockCopy(_M, i*N*sizeof(float), buf, 0, buf.Length);
                    stream.Write(buf, 0, buf.Length);
                }
            }
        }

        /// <summary>
        /// Deserialise a matrix using Buffer.BlockCopy to ensure it is FAST. This can deserialise an N=7200 matrix in around 15ms.
        /// Format is 4 byte integer containing the matrix order (N), LSB first. The following bytes are the float binary format
        /// with 4 bytes per float (single) for each array item in turn.
        /// NOTE: the static method returning an FMatrix might look bad, but it actually returns an object pointer into the global heap.
        /// DirtySerialise and DirtyDeserialise are opposite functions.
        /// </summary>
        /// <param name="Filename"></param>
        /// <returns></returns>
        public static FMatrix DirtyDeserialise(string Filename)
        {
            FMatrix Mat;
            using (Stream stream = File.OpenRead(Filename))
            {
                //read the little endian value of M and N back from the stream a byte at a time
                int M = stream.ReadByte() | (stream.ReadByte() << 8) | (stream.ReadByte() << 16) | (stream.ReadByte() << 24);
                int N = stream.ReadByte() | (stream.ReadByte() << 8) | (stream.ReadByte() << 16) | (stream.ReadByte() << 24);
                //int N = M;
                //now create a new matrix of order MxN, along with the byte buffer to read the line
                Mat = new FMatrix(M,N);
                byte[] buf = new byte[N * sizeof(float)];
                for (int i = 0; i < M; i++)
                {
                    stream.Read(buf, 0, buf.Length);
                    Buffer.BlockCopy(buf, 0, Mat._M, i*N*sizeof(float), buf.Length);

                }
            }
            return Mat;
        }
        ///

        public  void WriteCSVMatrix(string OutFilename)
        {
            using (TextWriter writer = File.CreateText(OutFilename))
            {
                for (int i = 0; i < this.M; i++)
                {
                    for (int j = 0; j < this.N; j++)
                    {
                        if (j > 0) writer.Write(",");
                        writer.Write(this._M[i, j]);
                    }
                    writer.WriteLine();
                }
            }
        }




        /// 

        /// <summary>
        /// Return correlation value as per Mike's VB program
        /// </summary>
        /// <param name="X">First matrix</param>
        /// <param name="Y">Second matrix</param>
        /// <param name="Alpha">Returns alpha parameter from regression</param>
        /// <param name="Beta">Returns beta parameter from regression</param>
        /// <returns>Correlation factor</returns>
        public static float Correlate(ref FMatrix X, ref FMatrix Y, out float Alpha, out float Beta, out float Sum2)
        {
            //if X.MxN != Y.MxN then fail
            float n2=X.M*X.N; //number of items in matrix (nz in VB code)
            float SumX=0, SumY=0, SumXY=0, SumXX2=0, SumYY2=0;
            for (int i = 0; i < X.M; i++)
            {
                for (int j = 0; j < X.N; j++)
                {
                    SumX += X._M[i, j];
                    SumY += Y._M[i, j];
                    SumXY += X._M[i, j] * Y._M[i, j];
                    SumXX2 += X._M[i, j] * X._M[i, j];
                    SumYY2 += Y._M[i, j] * Y._M[i, j];
                }
            }

            float C1 = SumXY - (SumX * SumY) / n2;
            float Sx = SumXX2 - (SumX * SumX) / n2;
            float Sy = SumYY2 - (SumY * SumY) / n2;
            float CC = C1 / (float)Math.Sqrt(Sx*Sy);
            Beta = (n2 * SumXY - SumX * SumY) / (n2 * SumXX2 - SumX * SumX);
            Alpha = (SumY - Beta * SumX) / n2;

            float Sum=0;
            Sum2 = 0;
            int ZeroCount1 = 0, ZeroCount2=0;
            for (int i = 0; i < X.M; i++)
            {
                for (int j = 0; j < X.N; j++)
                {
                    float Z = Alpha + Beta * X._M[i, j];
                    //Sum += Math.Abs(Y._M[i, j] - Z) / Y._M[i, j];
                    Sum += Math.Abs(Y._M[i, j] - Z); //RWM
                    Sum2 += Math.Abs(Y._M[i, j] - X._M[i,j]); //RWM
                    if (X._M[i, j] < 0.0001f) ++ZeroCount1;
                    if (Y._M[i, j] < 0.0001f) ++ZeroCount2;
                }
            }
            System.Diagnostics.Debug.WriteLine("ZeroCount1=" + ZeroCount1 +" ("+ZeroCount1/n2*100+"%) ZeroCount2=" + ZeroCount2+" ("+ZeroCount2/n2*100+"%)");
            //return 100.0f * Sum / n2; //AbsError in VB
            Sum2 /= n2; //RWM
            return Sum / n2; //RWM
        }

        /// <summary>
        /// Second version of correlate based on distance from mean and square distance from mean
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static double Correlate2(ref FMatrix A, ref FMatrix B)
        {
            //predcondition - check A.M==B.M && A.N==B.N
            double SumA = 0, SumB = 0;
            for (int i = 0; i < A.M; i++)
            {
                for (int j = 0; j < A.N; j++)
                {
                    SumA += A._M[i, j];
                    SumB += B._M[i, j];
                }
            }
            double MeanA = SumA / (A.M * A.N);
            double MeanB = SumB / (B.M * B.N);
            System.Diagnostics.Debug.WriteLine("MeanA: " + MeanA + " MeanB: " + MeanB+" SumA: "+SumA+" SumB: "+SumB);

            double C1 = 0, C2 = 0, C3 = 0, C4 = 0;
            for (int i = 0; i < A.M; i++)
            {
                for (int j = 0; j < A.N; j++)
                {
                    //C1 += A._M[i, j] - MeanA;
                    //C2 += B._M[i, j] - MeanB;
                    C1 += (A._M[i, j] - MeanA) * (B._M[i, j] - MeanB);
                    C3 += Math.Pow(A._M[i, j] - MeanA, 2);
                    C4 += Math.Pow(B._M[i, j] - MeanB, 2);
                }
            }

            //
            //double D1 = 0, D2 = 0, D3 = 0, D4 = 0, D5 = 0;
            //double NM = 7201 * 7201;
            //for (int i = 0; i < A.M; i++)
            //{
            //    for (int j = 0; j < A.N; j++)
            //    {
            //        D1+=
            //    }
            //}
            //

            //double r = C1*C2/(Math.Sqrt(C3)*Math.Sqrt(C4));
            double r = C1 / (Math.Sqrt(C3) * Math.Sqrt(C4));
            return r;
        }

        /// <summary>
        /// CPU version of the GPU Row Sums
        /// </summary>
        /// <param name="M">The matrix to compute the row sums on</param>
        /// <returns>A vector of the row sums i.e. an Mx1 column vector from the matrix</returns>
        public static float[] cpuRowSums(ref FMatrix M)
        {
            float [] Result = new float[M.M];
            for (int i = 0; i < M.M; i++)
            {
                float Sum = 0;
                for (int j = 0; j < M.N; j++)
                {
                    Sum += M._M[i, j];
                }
                Result[i] = Sum;
            }
            return Result;
        }








        #endregion methods

        #region Parallel CPU Methods

        /// <summary>
        /// This computes column sums (Dj) for a matrix using parallel CPU optimisation.
        /// TODO: you could cache this and let the user flag the matrix as dirty if it changes?
        /// Also, you can change the internal calculation to use floats for additional precision, but it takes longer and doesn't add that much precision.
        /// </summary>
        /// <returns>An array of column sums</returns>
        public float[] ComputeDj()
        {
            Stopwatch timer = Stopwatch.StartNew();
            float[] Dj = new float[N];
            for (int j = 0; j <N; j++)
            {
                //use range partition to split partial sum chunks across available CPUs
                object lockObject = new object();
                float sum = 0;
                var rangePartitioner = Partitioner.Create(0, M);

                Parallel.ForEach(
                    rangePartitioner,
                    () => 0.0f, //local partial result
                    //loop body
                    (range, loopState, initialValue) =>
                    {
                        float partialSum = initialValue;
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            partialSum += _M[i, j];
                        }
                        return partialSum;
                    },

                    //final step
                    (localPartialSum) =>
                    {
                        lock (lockObject)
                        {
                            sum += localPartialSum;
                        }
                    }
                );
                Dj[j] = sum;
            }
            System.Diagnostics.Debug.WriteLine("FMatrix ComputeDj: " + timer.ElapsedMilliseconds + " ms");
            return Dj;
        }

        /// <summary>
        /// This computes row sums (Oi) for a matrix using parallel CPU computation.
        /// See ComputeDj.
        /// </summary>
        /// <returns>An array of Oi values.</returns>
        public float[] ComputeOi()
        {
            Stopwatch timer = Stopwatch.StartNew();
            float[] Oi = new float[N];
            for (int i = 0; i < M; i++)
            {
                //use range partition to split partial sum chunks across available CPUs
                object lockObject = new object();
                float sum = 0;
                var rangePartitioner = Partitioner.Create(0, N);

                Parallel.ForEach(
                    rangePartitioner,
                    () => 0.0f, //local partial result
                    //loop body
                    (range, loopState, initialValue) =>
                    {
                        float partialSum = initialValue;
                        for (int j = range.Item1; j < range.Item2; j++)
                        {
                            partialSum += _M[i, j];
                        }
                        return partialSum;
                    },

                    //final step
                    (localPartialSum) =>
                    {
                        lock (lockObject)
                        {
                            sum += localPartialSum;
                        }
                    }
                );
                Oi[i] = sum;
            }
            System.Diagnostics.Debug.WriteLine("FMatrix ComputeOi: " + timer.ElapsedMilliseconds + " ms");
            return Oi;
        }

        #endregion Parallel CPU Methods

        
    }
}