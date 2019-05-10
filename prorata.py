#Prorata from Geography G1 to geograpy G2; G2 \in G1
import pandas as pd
import sqlite3
from sqlite3 import Error

def create_connection(db_file):
    """ create a database connection to the SQLite database
        specified by db_file
    :param db_file: database file
    :return: Connection object or None
    """
    try:
        conn = sqlite3.connect(db_file)
        return conn
    except Error as e:
        print(e)
 
    return None

####
## 1. Prepare files
####
#create conn database file
def prepare_files(Path,keyfile,G1,G2):
	#read key file
	print("Reading keyfile file...")
	df_key = pd.read_csv(Path+keyfile)

	#read  G1 data
	print("Reading G1 file...")
	df_A = pd.read_csv(Path+G1) 

	#read G2 data
	print("Reading G2 file...")
	df_B = pd.read_csv(Path+G2) 

	print ('\n We should have 4 columns in the keyfile:')
	print(df_key.shape)
	#Print first 5 lines of the OD dataframe
	print ('\n The first five lines of keyfile are:')
	print(df_key.head())
	print ('\n We should have at least 2 columns and xxx rows in G1:')
	print(df_A.shape)
	#Print first 5 lines of the OD dataframe
	print ('\n The first five lines of  G1 are:')
	print(df_A.head())

	print ('\n We should have at least 2 columns and xxx rows in G2:')
	print(df_B.shape)
	#Print first 5 lines of the OD dataframe
	print ('\n The first five lines of  G2 are:')
	print(df_B.head())

	return df_key,df_A,df_B

def create_tables(conn,cols,df_kf,df_g1,df_g2):
    """ create a table from a sting
    :param conn: Connection object
    :param create_table_sql: a CREATE TABLE statement
    :return:
    """
    try:
        c = conn.cursor()
        nskf=df_kf.columns.tolist() #columns in keyfile. We need to get the same names between file-db
        gltog2_1="CREATE TABLE IF NOT EXISTS g1tog2 ("+nskf[0]+" varchar(20),"+nskf[1]+" varchar(200),"+nskf[2]+" varchar(20),"+nskf[3]+" varchar(200),"
        gltog2_2=""
        for i in range(cols):
        	gltog2_2=gltog2_2+"data"+str(i)+" float,"
        	df_kf["data"+str(i)]=0.0
        gltog2_2=gltog2_2[0:len(gltog2_2)-1]+");"
        gltog=gltog2_1+gltog2_2
        print(gltog)
        #c.execute(gltog)
        #df_kf.to_sql('g1tog2',con=conn,if_exists='append',index=False)
        nsg1=df_g1.columns.tolist() #columns in g1. We need to get the same names between file-db
        lstg1=nsg1[0]+" varchar (200),"
        for i in range(len(nsg1)-1):
        	lstg1=lstg1+nsg1[i+1]+" float,"
        lstg1=lstg1[0:len(lstg1)-1]
        gltog1="CREATE TABLE IF NOT EXISTS g1 ("+lstg1+")"
        print(gltog1)
        c.execute(gltog1)        
        nsg2=df_g2.columns.tolist() #columns in g2. We need to get the same names between file-db
        lstg2=nsg2[0]+" varchar (200),"
        for i in range(len(nsg2)-1):
        	lstg2=lstg2+nsg2[i+1]+" float,"
        lstg2=lstg2[0:len(lstg2)-1]
        gltog2="CREATE TABLE IF NOT EXISTS g2 ("+lstg2+")"
        print(gltog2)
        c.execute(gltog2) 

        df_g1.to_sql('g1',con=conn,if_exists='append',index=False)
        df_g2.to_sql('g2',con=conn,if_exists='append',index=False)
    except Error as e:
        print(e)

def join_tables(conn,nskf,nsg1,nsg2,Path):
	 c = conn.cursor()
	 #a=c.execute("PRAGMA table_info(g1tog2)").fetchall()
	 #print(a[0][1])
	 lst=""
	 for i in range(len(nsg1)-1):
	 	lst=lst+"g1."+nsg1[i+1]+" as data"+str(i+1)+",g2."+nsg2[i+1]+" as data"+str(i+2)+","
	 lst=lst[0:len(lst)-1]
	 print(lst)	
	 sql="select g1tog2."+nskf[0]+",g1tog2."+nskf[2]+","+lst+" from g1tog2,g2 inner join g1 on g1tog2."+nskf[0]+"=g1."+nsg1[0]+" where g2."+nsg2[0]+"=g1tog2."+nskf[2]+";"	
	 #sql="select g1tog2."+nskf[0]+",g1tog2."+nskf[2]+",g1."+nsg1[1]+" as data1,g2."+nsg2[1]+" as data2 from g1tog2,g2 inner join g1 on g1tog2."+nskf[0]+"=g1."+nsg1[0]+" where g2."+nsg2[0]+"=g1tog2."+nskf[2]+";"
	 print(sql)
	 df = pd.read_sql(sql,conn)
	 df['Prorata'] = (df['data2']*100)/df['data1']

	 return df
	

def main():
	Path = "D:\\quant-data\\cambridge\\"
	database = Path+"geogProrata.db"
	print("Reading database file..."+Path+"geogProrata.db")
	conn=create_connection(database)
	#key file
	keyfile="keytable.csv"
	#G1 file
	G1="G1.csv"
	#G2 file
	G2="G2.csv"
	#Open files and assign them to a dataframe
	df_kf,df_g1,df_g2 = prepare_files(Path,keyfile,G1,G2)
	colsg1 = df_g1.shape[1]
	colsg2 = df_g2.shape[1]
	if colsg1!=colsg2:
		print("Files should have same number of columns")
		exit()

	#create key,G1 and G2 tables in the db
	#create_tables(conn,colsg1,df_kf,df_g1,df_g2)
	nskf=df_kf.columns.tolist()
	nsg1=df_g1.columns.tolist()
	nsg2=df_g2.columns.tolist()
	df=join_tables(conn,nskf,nsg1,nsg2,Path)

	df.to_csv(Path+"G1_G2.csv")
	conn.close()
if __name__ == '__main__':
    main()
    print("Done....")
