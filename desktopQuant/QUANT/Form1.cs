using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace QUANT
{
   
    public partial class Form1 : Form
    {
        public int modelType = 1;
        public int nexperiments = 1;
        public Form1()
        {
            InitializeComponent();
        }


        private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            Console.WriteLine("test"); // <-- For debugging use.
        }

        private void button1_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                //fbd.SelectedPath = @"D:\desktopQuant\data\";
                fbd.SelectedPath = @"D:\2020\grantBikes\data\tranus\";
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    string[] files = Directory.GetFiles(fbd.SelectedPath);
                    /*System.Windows.Forms.MessageBox.Show("Directory: " + fbd.SelectedPath.ToString(), "Message");
                    System.Windows.Forms.MessageBox.Show("Files found: " + files.Length.ToString(), "Message");
                    for (int i=0;i< files.Length;i++)
                        System.Windows.Forms.MessageBox.Show("Files found: " + files.ElementAt(i), "Message");*/

                    // Display the ProgressBar control.
                    pBar1.Visible = true;
                    pBar1.Minimum = 1;
                    pBar1.Maximum = 100;
                    pBar1.Value = 1;
                    pBar1.Step = 10;
                    FMatrix[] temp = new FMatrix[3]; //Data input to model.
                    FMatrix[] mtotal = null;
                    FMatrix[] mtemp = null;
                    mtotal = new FMatrix[3];
                    mtemp = new FMatrix[3];
                    string[] OutTPred = { "TPred1.csv", "TPred2.csv", "TPred3.csv" };
                    string OutTPredCombined = "TPred.csv";

                   int N = 194;
                   if (modelType == 3)
                    {
                        string dijprivate = "privateTimeCode.csv";
                        string dijprivateS = "dis_roads_min.bin";
                        string dijpublic = "publicTimeCode.csv";
                        string dijpublicS = "dis_bus_min.bin";
                        string tprivate = "viajesODprivadosM.csv";
                        string tpublicS = "TObs_1.bin";
                        string tpublic = "viajesODpublicosM.csv";
                        string tprivateS = "TObs_2.bin";
                        FMatrix dijPrivate = FMatrix.GenerateTripsLondon(fbd.SelectedPath + "\\" + dijprivate,N);
                        dijPrivate.DirtySerialise(fbd.SelectedPath + "\\" + dijprivateS);
                        FMatrix dijPublic = FMatrix.GenerateTripsLondon(fbd.SelectedPath + "\\" + dijpublic, N);
                        dijPublic.DirtySerialise(fbd.SelectedPath + "\\" + dijpublicS);
                        FMatrix tprivateM = FMatrix.GenerateTripsLondon(fbd.SelectedPath + "\\" + tprivate, N);
                        tprivateM.DirtySerialise(fbd.SelectedPath + "\\" + tpublicS);
                        FMatrix tpublicM = FMatrix.GenerateTripsLondon(fbd.SelectedPath + "\\" + tpublic, N);
                        tpublicM.DirtySerialise(fbd.SelectedPath + "\\" + tprivateS);
                    }
                    
                    int NumModes = 1;
                    switch (this.modelType)
                    {
                        case 3:
                            NumModes = 2; break;
                        default:
                            NumModes = 3; break;
                    }
                    QUANT3ModelProperties q = new QUANT3ModelProperties();

                    FMatrix[] TObs = new FMatrix[NumModes]; //Data input to model.
                    for (int k = 0; k < NumModes; k++)
                    {
                        TObs[k] = FMatrix.DirtyDeserialise(fbd.SelectedPath + "\\" + q.InTObs[k]);
                    }
                    for (int k = 0; k < NumModes; k++)
                      {
                          mtotal[k] = new FMatrix(N, N);
                      }


                    for (int i = 0; i < nexperiments; i++)
                    {
                        QUANT3Model quant = new QUANT3Model(modelType);
                        
                        temp = quant.LoadAndRun(q, fbd, pBar1, i);
                        //quant.LoadAndRun(q, fbd, pBar1);
                        //mtemp = temp;

                        for (int j = 0; j < NumModes; j++)
                        {
                                //mtemp[j] = temp[j];
                                //mtemp[j].DirtySerialise(fbd.SelectedPath + "\\" + OutTPred[j] + "_1");
                                temp[j].WriteCSVMatrix(fbd.SelectedPath + "\\" + OutTPred[j] + "_" +i.ToString() +  ".csv");
                        }

                        FMatrix TPredCombinedtmp = new FMatrix(N, N);
                        float Sum = 0;
                        for (int w = 0; w < TPredCombinedtmp.M; w++)
                        {
                            for (int j = 0; j < TPredCombinedtmp.N; j++)
                            {
                                for (int k = 0; k < NumModes; k++)
                                {
                                    Sum += temp[k]._M[w, j];
                                }
                                TPredCombinedtmp._M[w, j] = Sum;
                                Sum = 0.0f;
                            }
                        }
                        TPredCombinedtmp.WriteCSVMatrix(fbd.SelectedPath + "\\TPred_" + i.ToString() + ".csv");

                        for (int j = 0; j < NumModes; j++)
                        {
                           mtotal[j] = mtotal[j].SumMatrix(temp[j],N);
                        }
                      System.Diagnostics.Debug.WriteLine("run = " + i);
                    }

                    for (int j = 0; j < NumModes; j++)
                    {
                        mtotal[j] =  mtotal[j].Scale(1.0f/nexperiments);
                    }


                    for (int k = 0; k < NumModes; k++)
                    {
                        //mtotal[k].DirtySerialise(fbd.SelectedPath + "\\" + OutTPred[k]);
                        mtotal[k].WriteCSVMatrix(fbd.SelectedPath + "\\" + OutTPred[k]);
                    }

                    FMatrix TPredCombined = new FMatrix(N,N);
                    for ( int i = 0; i < TPredCombined.M; i++)
                    {
                        for (int j = 0; j < TPredCombined.N; j++)
                        {
                            float Sum = 0;
                            for (int k = 0; k < NumModes; k++)
                            {
                                Sum += mtotal[k]._M[i, j];
                            }
                            TPredCombined._M[i, j] = Sum;
                        }
                    }
                    //TPredCombined.DirtySerialise(fbd.SelectedPath + "\\" + OutTPredCombined);
                    TPredCombined.WriteCSVMatrix(fbd.SelectedPath + "\\" + OutTPredCombined);

                    }
                button2.Enabled = true;
            }
            /*int size = -1;
            DialogResult result = openFileDialog1.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                string file = openFileDialog1.FileName;
                try
                {
                    string text = File.ReadAllText(file);
                    size = text.Length;
                }
                catch (IOException)
                {
                }
            }
            Console.WriteLine(size); // <-- Shows file size in debugging mode.
            Console.WriteLine(result); // <-- For debugging use.*/

        }

        private void button2_Click(object sender, EventArgs e)
        {
            Form2 f2 = new Form2();
            f2.ShowDialog();
        }

        private void radioButton_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton rb = sender as RadioButton;

            if (rb == null)
            {
                MessageBox.Show("Sender is not a RadioButton");
                modelType = -1;
                return;
            }
            if (radioButton1.Checked == true)
            {
                MessageBox.Show("Original QUANT !! ");
                modelType = 1;
                return;
            }
            else if (radioButton2.Checked == true)
            {
                MessageBox.Show("Cambridge QUANT !! ");
                modelType = 2;
                return;
            }
            else
            {
                MessageBox.Show("Mexico QUANT !! ");
                modelType = 3;
                return;
            }
        }
    }
}
