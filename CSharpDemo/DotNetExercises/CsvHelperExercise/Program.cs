using CsvHelper;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace CsvHelperExercise
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var students = new List<StudentInfo>
            {
                new StudentInfo { ID = 1, Name = "张三", Age = 20, Class = "终极一班", Gender = "男", Address = "北京市东城区" },
                new StudentInfo { ID = 2, Name = "李四", Age = 21, Class = "终极一班", Gender = "女", Address = "上海市黄浦区" },
                new StudentInfo { ID = 3, Name = "王五", Age = 22, Class = "终极一班", Gender = "男", Address = "广州市越秀区" },
                new StudentInfo { ID = 4, Name = "赵六", Age = 20, Class = "终极二班", Gender = "女", Address = "深圳市福田区" },
                new StudentInfo { ID = 5, Name = "孙七", Age = 23, Class = "终极二班", Gender = "男", Address = "杭州市西湖区" },
                new StudentInfo { ID = 6, Name = "周八", Age = 24, Class = "终极二班", Gender = "女", Address = "南京市玄武区" },
                new StudentInfo { ID = 7, Name = "吴九", Age = 22, Class = "终极二班", Gender = "男", Address = "成都市锦江区" },
                new StudentInfo { ID = 8, Name = "小袁", Age = 21, Class = "终极三班", Gender = "女", Address = "重庆市渝中区" },
                new StudentInfo { ID = 9, Name = "大姚", Age = 20, Class = "终极三班", Gender = "男", Address = "武汉市武昌区" },
                new StudentInfo { ID = 10, Name = "追逐时光者", Age = 23, Class = "终极三班", Gender = "女", Address = "长沙市天心区" },
                new StudentInfo { ID = 11, Name = "陈十一", Age = 22, Class = "终极四班", Gender = "男", Address = "天津市和平区" },
                new StudentInfo { ID = 12, Name = "黄十二", Age = 21, Class = "终极四班", Gender = "女", Address = "西安市雁塔区" },
                new StudentInfo { ID = 13, Name = "刘十三", Age = 24, Class = "终极四班", Gender = "男", Address = "苏州市姑苏区" },
                new StudentInfo { ID = 14, Name = "郑十四", Age = 20, Class = "终极四班", Gender = "女", Address = "东莞市莞城区" },
                new StudentInfo { ID = 15, Name = "冯十五", Age = 23, Class = "终极五班", Gender = "男", Address = "佛山市禅城区" },
                new StudentInfo { ID = 16, Name = "褚十六", Age = 25, Class = "终极五班", Gender = "女", Address = "厦门市思明区" },
                new StudentInfo { ID = 17, Name = "卫十七", Age = 22, Class = "终极五班", Gender = "男", Address = "青岛市市南区" },
                new StudentInfo { ID = 18, Name = "蒋十八", Age = 21, Class = "终极五班", Gender = "女", Address = "大连市中山区" },
                new StudentInfo { ID = 19, Name = "沈十九", Age = 24, Class = "终极六班", Gender = "男", Address = "宁波市海曙区" },
                new StudentInfo { ID = 20, Name = "韩二十", Age = 20, Class = "终极六班", Gender = "女", Address = "温州市鹿城区" }
            };

            //定义 CSV 文件路径
            var filePath = @".\StudentInfoFile.csv";
            var manualFilePath = @".\StudentInfoFile_Manual.csv";

            //不使用csvHelper库，手动写入 CSV 文件数据
            //可以采用反射来遍历或者直接使用字符串拼接的方式来构建 CSV 文件内容
            WriteCsvManually(manualFilePath, students);
            Console.WriteLine($"手动写入 CSV 文件完成: {manualFilePath}");
            // 手动读取 CSV 文件数据（使用反射）
            var manualStudents = ReadCsvManually<StudentInfo>(manualFilePath);
            Console.WriteLine($"手动读取到 {manualStudents.Count} 条记录");



            //-----对比一下
            //写入 CSV 文件数据（使用 CsvHelper）
            using (var writer = new StreamWriter(filePath))
            using (var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csvWriter.WriteRecords(students);
            }
            Console.WriteLine($"CsvHelper 写入 CSV 文件完成: {filePath}");

            //读取 CSV 文件数据
            using (var reader = new StreamReader(filePath))
            using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var getStudentInfos = csvReader.GetRecords<StudentInfo>().ToList();
                Console.WriteLine($"CsvHelper 读取到 {getStudentInfos.Count} 条记录");
            }
        }

        /// <summary>
        /// 使用反射手动写入 CSV 文件
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="filePath">文件路径</param>
        /// <param name="records">要写入的记录集合</param>
        static void WriteCsvManually<T>(string filePath, IEnumerable<T> records)
        {
            var csvContent = new StringBuilder();

            // 使用反射获取属性信息
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // 写入表头（属性名）
            var headers = properties.Select(p => p.Name);
            csvContent.AppendLine(string.Join(",", headers));

            // 写入数据行
            foreach (var record in records)
            {
                var values = properties.Select(p =>
                {
                    var value = p.GetValue(record)?.ToString() ?? string.Empty;
                    // 如果值包含逗号、引号或换行符，需要用引号包裹并转义
                    if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                    {
                        value = $"\"{value.Replace("\"", "\"\"")}\"";
                    }
                    return value;
                });
                csvContent.AppendLine(string.Join(",", values));
            }

            // 写入 CSV 文件
            File.WriteAllText(filePath, csvContent.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 使用反射手动读取 CSV 文件
        /// </summary>
        static List<T> ReadCsvManually<T>(string filePath) where T : new()
        {
            var result = new List<T>();
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);

            if (lines.Length == 0) return result;

            // 获取表头
            var headers = lines[0].Split(',');
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // 创建属性名到属性的映射
            var propertyMap = properties.ToDictionary(p => p.Name, p => p);

            // 读取数据行
            for (int i = 1; i < lines.Length; i++)
            {
                var values = lines[i].Split(',');
                var obj = new T();

                for (int j = 0; j < headers.Length && j < values.Length; j++)
                {
                    if (propertyMap.TryGetValue(headers[j], out var property))
                    {
                        var value = values[j];
                        // 类型转换
                        var convertedValue = Convert.ChangeType(value, property.PropertyType);
                        property.SetValue(obj, convertedValue);
                    }
                }

                result.Add(obj);
            }

            return result;
        }
    }
}
