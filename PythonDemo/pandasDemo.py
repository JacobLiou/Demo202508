import pandas as pd

# df = pd.read_csv('data.csv')
# print(df.head())


# df = pd.DataFrame({
#     'Name': ['Alice', 'Bob', 'Charlie', 'David'],
#     'Age': [24, 30, 22, 35],
#     'City': ['New York', 'Los Angeles', 'Chicago', 'Houston']
# })

# print(df)

# df.to_csv('output.csv', index=False)


df = pd.read_csv('output.csv')
# print(df['Age'])

# print(df.iloc[1,2])

# print(df.loc[1,'City'])

# df['Age'].fillna(df['Age'].mean(), inplace=True)

print(df.groupby('City')['Age'].agg(['count', 'mean', 'max']))


left = pd.DataFrame({'id':[1,2], 'score':[80,90]})
right = pd.DataFrame({'id':[1,2], 'level':['A','B']})
merged = pd.merge(left, right, on='id', how='inner')
print(merged)