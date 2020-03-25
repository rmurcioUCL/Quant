using System;
using System.IO;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Runtime.Serialization.Formatters.Binary;

namespace QUANT
{
    //Serialise and deserialise objects
    public class Serialiser
    {
        public static object Get(string Filename) {
            object ob = null;
            using (Stream stream = File.OpenRead(Filename)) {
                BinaryFormatter serializer = new BinaryFormatter();
               ob =  serializer.Deserialize(stream);
            }
            return ob;
        }

        public static void Put(string Filename, object ob) {
            using (Stream stream = File.Create(Filename))
            {
                BinaryFormatter serializer = new BinaryFormatter();
                serializer.Serialize(stream,ob);
            }
        }

        /// <summary>
        /// Load a CSV file into a data table
        /// </summary>
        /// <param name="Filename"></param>
        public static DataTable LoadCSV(string Filename) {
            DataTable dt = new DataTable(Path.GetFileNameWithoutExtension(Filename));
            using (StreamReader reader = File.OpenText(Filename))
            {
                string Line = reader.ReadLine();
                string[] Header = Line.Split(new char[] { ',' }); //OK, so it's not going to like commas in quotes

                //read the first line of real data and use it to infer the column types
                Line = reader.ReadLine();
                string[] Fields = Line.Split(new char[] { ',' });
                //set the column names and types using the first line of real data
                for (int i = 0; i < Fields.Length; i++)
                {
                    float f;
                    if (float.TryParse(Fields[i], out f))
                        dt.Columns.Add(Header[i], typeof(float));
                    else
                        dt.Columns.Add(Header[i], typeof(string));
                }

                //and don't forget to add the first row of data, then the rest of the lines
                //From the exising line first, which we've just read, then the existing fields - NOTE use of DataFields
                do
                {
                    string [] DataFields = Line.Split(new char[] { ',' });
                    DataRow row = dt.NewRow();
                    for (int i = 0; i < dt.Columns.Count; i++)
                    {
                        DataColumn col = dt.Columns[i];
                        if (col.DataType == typeof(float))
                            row[i] = Convert.ToSingle(DataFields[i]);
                        else
                            row[i] = DataFields[i];
                    }
                    dt.Rows.Add(row);

                } while ((Line = reader.ReadLine()) != null);
            }
            return dt;
        }

       

    }
}