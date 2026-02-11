using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X_Plane2Influx
{
    public class InfluxLayer
    {
        InfluxDbClient influxDbClient;

        public bool isConnected { get; private set; }

        int connecErrorCumulative = 0;

        DateTime lastDbTryConnect;

        public InfluxLayer()
        {
            if (influxDbClientConecta())
            {

            }

        }

        public bool influxDbClientConecta()
        {
            bool ret = false;

            try
            {
                lastDbTryConnect = DateTime.Now;

                influxDbClient = new InfluxDbClient("http://marcelocampos.dev.br:8086", "CSBlueCode", Environment.GetEnvironmentVariable("InfluxDbPass", EnvironmentVariableTarget.User) , InfluxDbVersion.v_1_3);

                if (influxDbClient != null)
                {
                    Debug.WriteLine("[" + millis() + "] " + "> InfluxDb Conectado Ok");
                    isConnected = true;
                    connecErrorCumulative = 0;
                    ret = true;
                }
                else
                {
                    isConnected = false;
                    Debug.WriteLine("[" + millis() + "] " + "> InfluxDb NÃO Conetado... ");
                }

            }
            catch (Exception ex)
            {
                isConnected = false;
                Debug.WriteLine("[" + millis() + "] " + "> Erro Conectando...: " + ex.Message);
            }

            return ret;
        }

        public async Task<bool> WriteAsync(InfluxData.Net.InfluxDb.Models.Point pointToWrite, string dbName)
        {
            bool ret = false;


            if (connecErrorCumulative >= 3 && DateTime.Now - lastDbTryConnect >= new TimeSpan(0, 0, 5))
            {
                influxDbClientConecta();
            }

            try
            {
                List<string> dbList = await ExecuteRequestAsync();

                if (dbList == null)
                {
                    Debug.WriteLine("[" + millis() + "] " + "> ERRO WriteAsync(): dbList null ..." + dbName + " existe ?)");
                    connecErrorCumulative++;
                }
                else
                {
                    if (!dbList.Contains(dbName))
                    {
                        var resCreateDb = await influxDbClient.Database.CreateDatabaseAsync(dbName);
                        Debug.WriteLine("[" + millis() + "] " + "> Banco de dados " + dbName + " não existe, criando...");

                        if (!resCreateDb.Success)  // Erro no acesso ao db :'-(
                        {
                            ret = false;
                            connecErrorCumulative++;
                        }
                    }
                    else
                    {
                        var response = await influxDbClient.Client.WriteAsync(pointToWrite, dbName);

                        if (response.Success)
                        {
                            connecErrorCumulative = 0;
                            ret = true;
                        }
                    }
                }

            }
            catch (Exception)
            {
                Debug.WriteLine("[" + millis() + "] " + "> ERRO WriteAsync() ...");
                connecErrorCumulative++;
            }

            return ret;
        }

        public async Task<List<string>> ExecuteRequestAsync()
        {
            List<string> dbList = new List<string>();

            var databaseNames = await GetDatabaseNamesAsync();

            if (databaseNames == null)
                return null;

            foreach (var dbName in databaseNames) dbList.Add(dbName);

            return dbList;
        }

        public async Task<IEnumerable<string>> GetDatabaseNamesAsync()
        {

            try
            {
                var databases = await influxDbClient.Database.GetDatabasesAsync().ConfigureAwait(false);
                return (from db in databases select db.Name);
            }
            catch (Exception)
            {
                Debug.WriteLine("[" + millis() + "] " + "> ERRO GetDatabaseNamesAsync() ...Banco de dados ");

            }

            return null;
        }

        private String millis()
        {
            return DateTime.Now.TimeOfDay.ToString(); // Não mais milisegs ...   
        }

    }
}
