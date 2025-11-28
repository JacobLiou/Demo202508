import pandas as pd

# df = pd.read_csv('sales.csv')

# summary = df.groupby('region').agg(['sum', 'mean', 'max'])

# print(summary)

df = pd.read_csv('sales.csv')
# 读取数据并基本清洗
df['date'] = pd.to_datetime(df['date'], errors='coerce')
df['total'] = df['price'] * df['qty']
summary = df.groupby('region')['total'].agg(['sum', 'mean', 'max'])
print(summary)
#去除关键缺失
df.dropna(subset=['region', 'product'], inplace=True)

# grp = df.groupby(['region', 'product'])['totoal'].agg(['sum', 'mean', 'max'])