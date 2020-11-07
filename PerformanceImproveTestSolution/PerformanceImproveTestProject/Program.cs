using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceImproveTestProject
{
    class Program
    {
        public enum TypeExecution
        {
            PerformQuery,
            Setup
        }

        public class Dado
        {
            public Int32 ID;
            public string PayLoad;
        }

        public abstract class PerformanceTest
        {
            private List<List<Dado>> dados = new List<List<Dado>>();
            private List<TimeSpan> execTime = new List<TimeSpan>();
            private TimeSpan setupTime;

            private Stopwatch watch = new Stopwatch();

            protected abstract void OnSetup(List<Dado> original);

            protected abstract List<Dado> OnPerformQuery(List<Int32> keys);

            protected abstract string getName();

            public void Setup(List<Dado> original)
            {
                watch.Start();
                OnSetup(original);
                watch.Stop();
                setupTime = watch.Elapsed;
            }

            public void PerformQuery(List<Int32> keys)
            {
                watch.Start();
                dados.Add(OnPerformQuery(keys));
                watch.Stop();
                execTime.Add(watch.Elapsed);
            }

            public string GenerateHash(List<Dado> valores)
            {
                string serialized = valores.OrderBy(x => x.ID).Select(x => x.ID.ToString()).Aggregate((x, y) => x + ";" + y); 
                SHA256 sha256Hash = SHA256.Create();
                var data = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(serialized));
                var sBuilder = new StringBuilder();
                for (int i = 0; i < data.Length; i++)
                {
                    sBuilder.Append(data[i].ToString("x2"));
                }
                return sBuilder.ToString();
            }

            public void print()
            {                
                Console.WriteLine($"{getName()} | {setupTime} ");
                for (int i = 0; i < execTime.Count; i++)
                {
                    Console.WriteLine($"Exec:{execTime[i]} | Count:{dados[i].Count} |Hash:{GenerateHash(dados[i])}");
                }                
            }

            public static void PrintCSV(List<PerformanceTest> list)
            {
                StringBuilder s = new StringBuilder();
                s.AppendLine($"numero;nome;tempo;countResult;hash");
                list.ForEach(x =>
                {
                    for (int i = 0; i < x.execTime.Count; i++)
                    {
                        s.AppendLine($"{i};{x.getName()};{x.execTime[i].TotalMilliseconds};{x.dados[i].Count};{x.GenerateHash(x.dados[i])}");
                    }                    
                });
                Console.WriteLine(s.ToString());
            }
        }

        public abstract class LinqQuery : PerformanceTest
        {
            protected List<Dado> localDatabase;

            protected override void OnSetup(List<Dado> original)
            {
                localDatabase = original;
            }
        }

        public class QueryLinqWhere : LinqQuery
        {
            protected override string getName()
            {
                return "QueryLinqWhere";
            }

            protected override List<Dado> OnPerformQuery(List<int> keys)
            {
                return localDatabase.Where(x => keys.Exists(y => y==x.ID)).ToList();
            }

        }

        public class QueryLinqJoin : LinqQuery
        {
            protected override string getName()
            {
                return "QueryLinqJoin";
            }

            protected override List<Dado> OnPerformQuery(List<int> keys)
            {
                return localDatabase.Join(keys, x => x.ID, y => y, (x, y) => x).ToList();
            }

        }

        public class QueryLinqContain : LinqQuery
        {
            protected override string getName()
            {
                return "QueryLinqContain";
            }

            protected override List<Dado> OnPerformQuery(List<int> keys)
            {
                return localDatabase.Where(x => keys.Contains(x.ID)).ToList();
            }

        }

        public class QueryLinqContainParallel : LinqQuery
        {
            protected override string getName()
            {
                return "QueryLinqContainParallel";
            }

            protected override List<Dado> OnPerformQuery(List<int> keys)
            {
                return localDatabase.AsParallel().Where(x => keys.Contains(x.ID)).ToList();
            }

        }


        static void Main(string[] args)
        {
            List<PerformanceTest> SearchMethod = new List<PerformanceTest>();

            SearchMethod.Add(new QueryLinqWhere());
            SearchMethod.Add(new QueryLinqContain());
            SearchMethod.Add(new QueryLinqContainParallel());
            SearchMethod.Add(new QueryLinqJoin());
            

            List<Dado> original = new List<Dado>();
            int total = 100000;

            Console.WriteLine("Generating Data");
            for (int i = 0; i < total; i++)
            {
                original.Add(new Dado() { ID = i, PayLoad = "teste" });
            }

            Random rnd = new Random();
            List<List<Int32>> queryList = new List<List<int>>();
            Console.WriteLine("Generating IDs for Query");
            for (int i = 0; i < 10; i++)
            {
                queryList.Add(new List<int>());
                for (int j = 0; j < (total/100)*(i+1); j++)
                {
                    queryList[i].Add(rnd.Next(total));
                }
                queryList[i] = queryList[i].Distinct().ToList();
            }

            Console.WriteLine("Setup Method");
            SearchMethod.ForEach(x =>
            {
                x.Setup(original.ToList());
            });

            Console.WriteLine("Performing Tests");
            SearchMethod.ForEach(x =>
            {
                Console.WriteLine($"Performing: {x.ToString().Split('+')[1]}");
                queryList.ForEach(z =>
                {
                    x.PerformQuery(z);
                });
            });

            PerformanceTest.PrintCSV(SearchMethod);

            Console.Write("Enter para continuar...");
            Console.ReadLine();
        }
    }
}
