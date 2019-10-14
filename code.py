import pandas as pd
df = pd.read_csv('/Users/casa/Documents/quant/TPred1/TPred_10.csv')
dfb = pd.read_csv('/Users/casa/Documents/quant/0trainCode/0testCode.csv',names=['O','D','bus','road','rail'])
lst = dfb[['O','D']].values.tolist()
nlst = []
for index, row in df.iterrows():
    print(index)
    for i in range(1,984):
        if [row['O'],df.columns[i]] in lst:
            #print(row['O'],df.columns[i],row[df.columns[i]])
            nlst.append([row['O'],df.columns[i],row[df.columns[i]]])
ndf = pd.DataFrame(nlst)
ndf.to_csv('/Users/casa/Documents/quant/0trainCode/0roadtest.csv')

df = pd.read_csv('/Users/casa/Documents/quant/TPred1/TPred_20.csv')
dfb = pd.read_csv('/Users/casa/Documents/quant/0trainCode/0trainCode.csv',names=['O','D','bus','road','rail'])
lst = dfb[['O','D']].values.tolist()
nlst = []
for index, row in df.iterrows():
    print(index)
    for i in range(1,984):
        if [row['O'],df.columns[i]] in lst:
            #print(row['O'],df.columns[i],row[df.columns[i]])
            nlst.append([row['O'],df.columns[i],row[df.columns[i]]])
ndf = pd.DataFrame(nlst)
ndf.to_csv('/Users/casa/Documents/quant/0trainCode/0bustest.csv')

df = pd.read_csv('/Users/casa/Documents/quant/TPred1/TPred_30.csv')
dfb = pd.read_csv('/Users/casa/Documents/quant/0trainCode/0trainCode.csv',names=['O','D','bus','road','rail'])
lst = dfb[['O','D']].values.tolist()
nlst = []
for index, row in df.iterrows():
    print(index)
    for i in range(1,984):
        if [row['O'],df.columns[i]] in lst:
            #print(row['O'],df.columns[i],row[df.columns[i]])
            nlst.append([row['O'],df.columns[i],row[df.columns[i]]])
ndf = pd.DataFrame(nlst)
ndf.to_csv('/Users/casa/Documents/quant/0trainCode/0railtest.csv')