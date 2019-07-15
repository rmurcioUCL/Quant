import pandas as pd
import numpy as np

def CalculateCBar(matrix,dij):
	N,m = matrix.shape
	CNumerator=0.0
	CDenominator=0.0
	for i in range(1,N-1):
		for j in range(1,N-1):
			print(matrix.iloc[i,j])
			print(dij.iloc[i,j])
			CNumerator = CNumerator + float(matrix.iloc[i,j]) * float(dij.iloc[i,j])
			CDenominator = CDenominator + float(matrix.iloc[i,j])

	return  CNumerator / CDenominator

def main():
	#read Sm , Sn at LSOA and LSOA<->MSOA
	#inputP = "/Users/casa/Desktop/quant/"
	inputP = 'D:/QuantPython/Cambridge/'
	dfmaster = pd.read_csv(inputP+'masterCambridgetest.csv')
	print(dfmaster.shape)
	print(dfmaster.head())
	#read shortest path matrices by mode 
	dfdistroadL = pd.read_csv(inputP+'dis_roads_min.csv') #road LSAO 
	dfdistbusL = pd.read_csv(inputP+'dis_bus_min.csv') #bus LSOA
	dfdistroadM = pd.read_csv(inputP+'dis_roads_minMSOA.csv') #road MSOA
	dfdistbusM = pd.read_csv(inputP+'dis_bus_minMSOA.csv') #bus MSOA
	print(dfdistroadL.shape)
	print(dfdistbusL.shape)
	print(dfdistroadM.shape)
	print(dfdistbusM.shape)
	#read observed trips by mode MSOA
	dfnMode = pd.read_csv(inputP+'TObsMsoa.csv') #No mode
	dfroadM = pd.read_csv(inputP+'TObsMsoa_1.csv') #road
	dfbusM = pd.read_csv(inputP+'TObsMsoa_2.csv') #bus
	print(dfroadM.shape)
	print(dfbusM.shape)
	print(dfnMode.shape)
	a=zip(range(0,487),dfdistroadL.columns[1:])
	dfdistroadL.rename(index=dict(a), inplace=True)
	a=zip(range(0,487),dfdistbusL.columns[1:])
	dfdistbusL.rename(index=dict(a), inplace=True)
	a=zip(range(0,98),dfnMode.columns[1:])
	dfnMode.rename(index=dict(a), inplace=True)
	a=zip(range(0,98),dfroadM.columns[1:])
	dfroadM.rename(index=dict(a), inplace=True)
	a=zip(range(0,98),dfbusM.columns[1:])
	dfbusM.rename(index=dict(a), inplace=True)

	betas=[1.0,1.0]
	Converged = False
	while not Converged:
	    Tp = pd.DataFrame(index=dfdistroadL.columns[1:],columns=dfdistroadL.columns[1:])
	    #denominator
	    denominator=0.0
	    for lsoadistm in dfmaster.itertuples():
	        for lsoadistn in dfmaster.itertuples():
	            dmn = dfdistroadL.loc[lsoadistm.LSOACD,lsoadistn.LSOACD]
	            denominator=denominator+float(lsoadistm.Sm) * float(lsoadistm.Sn) * np.exp(-betas[0]*dmn) 
	    #numerator
	    for lsoaO in dfmaster.itertuples():
	        print(lsoaO.LSOACD)
	        for lsoaD in dfmaster.itertuples():
	            atractor=float(lsoaO.Sm)*float(lsoaD.Sn)*dfnMode.loc[lsoaO.MSOACD,lsoaD.MSOACD]
	            dmn = dfdistroadL.loc[lsoaO.LSOACD,lsoaD.LSOACD]
	            numerator=atractor * np.exp(-betas[0]*dmn)
	            Tp.loc[lsoaO.LSOACD,lsoaD.LSOACD]=float(numerator/denominator)
	    #calibration
	    CbarPred = CalculateCBar(Tp,dfdistroadL)
	    CbarObs = CalculateCBar(dfnMode,dfdistroadM)
	    delta = abs(CbarPred-CbarObs)
	    Converged = True
	    print(delta / CbarObs)
	    if delta / CbarObs > 0.1: #0.001:
	        betas[0]=beta[0]*CbarPred/CbarObs
	        Converged = False

if __name__ == '__main__':
    main()