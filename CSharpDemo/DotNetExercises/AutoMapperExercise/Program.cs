using AutoMapper;
using Mapster;
using System.Diagnostics;

namespace AutoMapperExercise
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== AutoMapper vs Mapster 对比演示 ===\n");

            var personInfo = new PersonInfo
            {
                FirstName = "大东",
                LastName = "陈",
                Age = 18,
                Nationality = "中国"
            };

            // ========== AutoMapper 映射 ==========
            Console.WriteLine("【AutoMapper 映射】");
            var configuration = new MapperConfiguration(cfg => {
                cfg.AddProfile<MappingProfile>();
                //或者下面这种方式
                //cfg.CreateMap<PersonInfo, PersonInfoDto>();
            });
            var mapper = configuration.CreateMapper();

            var personInfoDto = mapper.Map<PersonInfoDto>(personInfo);
            Console.WriteLine($"FirstName: {personInfoDto.FirstName}, LastName: {personInfoDto.LastName}, Age: {personInfoDto.Age}");

            // ========== Mapster 基本映射 ==========
            Console.WriteLine("\n【Mapster 基本映射】");
            var personInfoDto2 = personInfo.Adapt<PersonInfoDto>();
            Console.WriteLine($"FirstName: {personInfoDto2.FirstName}, LastName: {personInfoDto2.LastName}, Age: {personInfoDto2.Age}");

            // ========== Mapster 自定义映射配置 ==========
            Console.WriteLine("\n【Mapster 自定义映射配置】");
            // 配置自定义映射规则
            TypeAdapterConfig<PersonInfo, PersonInfoDto>.NewConfig()
                .Map(dest => dest.FirstName, src => $"【{src.FirstName}】") // 自定义转换
                .Map(dest => dest.Age, src => src.Age + 10); // 年龄加10

            var personInfoDto3 = personInfo.Adapt<PersonInfoDto>();
            Console.WriteLine($"FirstName: {personInfoDto3.FirstName}, Age: {personInfoDto3.Age}");

            // ========== Mapster 映射到已存在的对象 ==========
            Console.WriteLine("\n【Mapster 映射到已存在的对象】");
            var existingDto = new PersonInfoDto { FirstName = "原始名", LastName = "原始姓", Age = 0, Nationality = "未知" };
            Console.WriteLine($"映射前: FirstName: {existingDto.FirstName}, Age: {existingDto.Age}");
            
            // 重置配置以便演示
            TypeAdapterConfig<PersonInfo, PersonInfoDto>.Clear();
            personInfo.Adapt(existingDto);
            Console.WriteLine($"映射后: FirstName: {existingDto.FirstName}, Age: {existingDto.Age}");

            // ========== Mapster 使用全局配置 ==========
            Console.WriteLine("\n【Mapster 使用全局配置】");
            var config = new TypeAdapterConfig();
            config.NewConfig<PersonInfo, PersonInfoDto>()
                .Map(dest => dest.FirstName, src => src.LastName + src.FirstName); // 姓+名

            var personInfoDto4 = personInfo.Adapt<PersonInfoDto>(config);
            Console.WriteLine($"全名: {personInfoDto4.FirstName}");

            // ========== 性能对比 ==========
            Console.WriteLine("\n【性能对比 - 映射 10000 次】");
            const int iterations = 100000;

            // AutoMapper 性能测试
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                mapper.Map<PersonInfoDto>(personInfo);
            }
            sw.Stop();
            Console.WriteLine($"AutoMapper: {sw.ElapsedMilliseconds} ms");

            // Mapster 性能测试 (重置配置)
            TypeAdapterConfig<PersonInfo, PersonInfoDto>.Clear();
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                personInfo.Adapt<PersonInfoDto>();
            }
            sw.Stop();
            Console.WriteLine($"Mapster: {sw.ElapsedMilliseconds} ms");

            Console.WriteLine("\n=== 演示完成 ===");
        }
    }
}
