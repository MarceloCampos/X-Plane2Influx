using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
/*
 *  by Marcelo Campos
 *  Ver. 0.01 - Feb 2026
 *  
 *  Git:  https://github.com/MarceloCampos/
 *  Blog: https://marcelocampos.dev.br/
 *  
 *  Developed using Visual Studio 2026 Version: 18.1.1 / .NET 4.8.09221
 * 
 *  X-Plane 11 configuration:
 *      Menu Data Output : Network via UDP -> Selected items: 
 *        Descruption           | Index
 *        Speed..........       3 
 *        Coordinates....       20
 *        pitch rool headg..    17
 *        engine RPM.....       37
 *        Climb Stats (VSpeed)  132
 *        Yaw(Slip)......       18
 *        
 *  and click on "Send Network Data Output" to enable it and configure IP Address of machine that will receive (may be localhost or other machine on network)
 *        
 *        
 *        
 *  Please don´t use this code in production, this is only a PoC (Proof of Concept) and is provided "as is"
 *  and is licenced under MIT License:

Copyright (c) 2026 Marcelo Campos

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

 


 */
namespace X_Plane2Influx
{

    internal class Program
    {
        const byte XPLANE_BYTE_LAT_LONG = 0x14;
        static bool DBG_PACKET_UDP_OUT = false;
        static bool isNotToExit = false;

        public static bool DBG_PACKET_COO_OUT { get; private set; }
        public static bool DBG_PACKET_SPD_OUT { get; private set; }
        public static bool DBG_PACKET_PRH_OUT { get; private set; }
        public static bool DBG_PACKET_FND_OUT { get; private set; }
        public static bool DBG_PACKET_RPM_OUT { get; private set; }
        public static bool DBG_PACKET_VSP_OUT { get; private set; }
        public static bool DBG_PACKET_PLN_OUT { get; private set; }
        public static bool DBG_PACKET_YAW_OUT { get; private set; }


        public static bool RECORD_METRIC_W_TIMESTAMP = false;
        public static int portUdpServer { get; private set; }

        static int qtdPacketsInterval = 0;
        static int qtdPacketsIntervalOk = 0;
        static int qtdPacketsIntervalEr = 0;
        static int qtdMetricsInputBd = 0;

        private static string dbname = "X_PlaneOne";

        private static InfluxLayer influxLayer;

        public enum TipoData
        {
            velocidades,
            latLongAlt,
            packetHeader,
            ptchrollhdg,
            engineRpm,
            vspeed,
            slip /* yaw */
        }

        static List<byte[]> DataHeaderList = new List<byte[]>()
        {   // Importante: manter a seq pois está amarrada com o enum acima...
            /*Velocidades:  */ new byte[]{ 0x03, 0x00, 0x00, 0x00 } , 
            /*LatLongAlt :  */ new byte[]{ 0x14, 0x00, 0x00, 0x00 } ,
            /*packetHeader: */ new byte[]{ 0x44, 0x41, 0x54, 0x41 } ,
            /*pitch rool headg: */ new byte[]{ 0x11, 0x00, 0x00, 0x00 } ,
            /*engine RPM: */ new byte[]{ 0x25, 0x00, 0x00, 0x00 },
            /*VSpeed: */ new byte[]{ 0x84, 0x00, 0x00, 0x00 },
            /*Slip(Yaw): */ new byte[]{ 0x12, 0x00, 0x00, 0x00 }
        };

        static void Main(string[] args)
        {
            // DBG_PACKET_FND_OUT = true;
            // DBG_PACKET_COO_OUT = true;
            // DBG_PACKET_SPD_OUT = true;
            // DBG_PACKET_PRH_OUT = true;
            // DBG_PACKET_RPM_OUT = true;
            // DBG_PACKET_VSP_OUT = true;
            // DBG_PACKET_PLN_OUT = false;
            DBG_PACKET_YAW_OUT = false;

            DateTime dtLastInfoQtdMetric = DateTime.Now;
            int interval_consoleOutQtdMterics = 10;  // em segundos please ...

            AppInit();

            while (!isNotToExit)
            {
                Thread.Sleep(10);

                if (DateTime.Now - dtLastInfoQtdMetric >= new TimeSpan(0, 0, interval_consoleOutQtdMterics))
                {
                    Console.WriteLine(" [" +DateTime.Now.ToString() + "] Qtd Pacotes Recebidos: {0}, Ok/Erro: {1}/{2}, Measurements inputs BD: {3}  (intervalo {4}s)", qtdPacketsInterval.ToString(), qtdPacketsIntervalOk, qtdPacketsIntervalEr, qtdMetricsInputBd, interval_consoleOutQtdMterics);
                    
                    qtdPacketsInterval = 0;
                    qtdPacketsIntervalOk = 0;
                    qtdPacketsIntervalEr = 0;
                    qtdMetricsInputBd = 0;

                    dtLastInfoQtdMetric = DateTime.Now;
                }
            }

            Console.WriteLine(" Finalizada Aplicação ...");
        }
        private static void AppInit()
        {
            Console.Write(" Iniciando ...\r\n IP(s) ");


            IPAddress[] localIps = GetIp.GetLocalIPAddress();

            foreach (IPAddress item in localIps)
                if (item.AddressFamily == AddressFamily.InterNetwork) Console.Write(item.ToString() + " ");

            Console.WriteLine("");

            Console.WriteLine(" Host Name: " + Dns.GetHostName());
            Console.Write(" Métricas p/ Upload: " + DataHeaderList.Count.ToString() + ": ");

            for (int i = 0; i < DataHeaderList.Count; i++)
            {
                Console.Write(DataHeaderList[i][0].ToString() + "  ");
            }

            Console.WriteLine();

            influxLayer = new InfluxLayer();

            portUdpServer = 49001;

            startUdpServerThread(portUdpServer);

            if (!DBG_PACKET_FND_OUT)
                Console.WriteLine(" Init OK, Recebendo dados Porta: " + portUdpServer.ToString());
        }

        private static async void CallAirplaneWriteAsync()
        {

            InfluxData.Net.InfluxDb.Models.Point pointToWrite = new InfluxData.Net.InfluxDb.Models.Point()
            {
                Name = "AircraftMetrics", // serie/measurement/table to write into
                Tags = new Dictionary<string, object>()
                {
                    { "AircraftId", 1 },
                    { "SerialNumber", "0F14" }
                },
                Fields = new Dictionary<string, object>()
                {
                    { "IsValid", Airplane.IsValid },
                    { "Altitude", Airplane.Altitude },
                    { "AirSpeed", Airplane.AirSpeed },
                    { "GndSpeed", Airplane.GndSpeed },
                    { "WndSpeed", Airplane.WndSpeed },
                    { "Lat", Airplane.CoordLat },
                    { "Long", Airplane.CoordLng },
                    { "Heading", Airplane.Heading },
                    { "EngineRPM", Airplane.EngineRpm },
                    { "VerticalSpeed", Airplane.VSpeed },
                    { "Yaw", Airplane.Yaw },
                    { "Pitch", Airplane.Pitch },
                    { "Rool", Airplane.Roll },
                    { "HeadingTrue", Airplane.HeadingTrue },
                    { "HeadingMagn", Airplane.HeadingMagn }
                },

                // movido p/ condicional abaixo Timestamp = DateTime.UtcNow // optional (can be set to any DateTime moment)
            };

            if (RECORD_METRIC_W_TIMESTAMP)
                pointToWrite.Timestamp = DateTime.UtcNow;

            bool resultado = await influxLayer.WriteAsync(pointToWrite, dbname);
        }
        private static void startUdpServerThread(int port)
        {

            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                if (DBG_PACKET_FND_OUT)
                    Console.WriteLine("\r\n [" + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "] > Starting Udp Server Thread... port = " + port.ToString());

                udpServer(port);
            }).Start();

        }
        private static void udpServer(int UDP_LISTEN_PORT)
        {
            UdpClient udpServer = new UdpClient(UDP_LISTEN_PORT);
            DateTime lastTimeDbWrite = DateTime.Now;

            while (true)
            {

                var groupEP = new IPEndPoint(IPAddress.Any, 11000); // listen on any port
                var data = udpServer.Receive(ref groupEP);

                if (data != null)
                {
                    int dataLength = data.Length;

                    int position = searchForData(data, TipoData.packetHeader, 0);

                    if (position < 0)
                        continue;

                    if (DBG_PACKET_UDP_OUT)
                    {
                        for (int i = 0; i < dataLength; i++)
                        {
                            Debug.Write(data[i].ToString("X02"));
                            Debug.Write(' ');
                        }
                        Debug.WriteLine(" \r\n ");
                    }

                    //  startAnalizaRxUdpThread(data);
                    AnalizaRxUdp(data);

                    if (Airplane.IsValid && influxLayer.isConnected && DateTime.Now - lastTimeDbWrite >= new TimeSpan(0, 0, 0, 0, 500))
                    {


                        CallAirplaneWriteAsync();
                        lastTimeDbWrite = DateTime.Now;

                        qtdMetricsInputBd++;

                        if (DBG_PACKET_PLN_OUT)
                            Debug.WriteLine(" Airlane: RPM: {0}", Airplane.EngineRpm);
                    }

                }
                //    udpServer.Send(new byte[] { 1 }, 1); // if data is received reply letting the client know that we got his data          
            }
        }
        private static void startAnalizaRxUdpThread(byte[] data)
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                if (DBG_PACKET_FND_OUT)
                    Console.WriteLine("\r\n [" + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "] > Starting AnalizaRxUdpThread...");
                AnalizaRxUdp(data);
            }).Start();
        }
        private static int searchForData(byte[] data, TipoData qualTipo, int offSet)
        {   // encontra ponto de inicio do desejado passado em 'qualTipo'
            int vetor = -1;
            int i = 0;
            int ret = -1;
            int DataHeaderListSize = DataHeaderList[(int)qualTipo].Length;
            byte[] arrayBuscado = DataHeaderList[(int)qualTipo];

            if (data.Length < DataHeaderListSize)
                return -1;

            for (vetor = offSet; vetor < data.Length; vetor++)
            {
                if (data[vetor] != arrayBuscado[i])
                {
                    i = 0;
                    continue;
                }
                else
                {
                    if (++i == DataHeaderListSize)
                    {
                        ret = (vetor + 1);
                        //Debug.Print(" - Encontrado[" + ((int)qualTipo).ToString() + "] pointer= " + ret.ToString());

                        break;
                    }
                }
            }

            return ret;
        }
        private static void AnalizaRxUdp(byte[] data)
        {
            int indexDataHeaderList = 0;
            int indexLastFound = 0;
            int metricsFounds = 0;

            while (indexDataHeaderList < DataHeaderList.Count)
            {
                int indexFound = searchForData(data, (TipoData)indexDataHeaderList, indexLastFound);
                if (indexFound > 0) // maior que 0 pois 0 já deveria ser o do cabeçalho
                {
                    switch (indexDataHeaderList)
                    {
                        case (int)TipoData.velocidades:
                            if (DBG_PACKET_FND_OUT)
                                Debug.Print("\t - Encontrado TipoData.velocidades ! pointer= " + indexFound.ToString());
                            AnalizaVelocidades(data, indexFound);
                            metricsFounds++;
                            break;

                        case (int)TipoData.latLongAlt:
                            if (DBG_PACKET_FND_OUT)
                                Debug.Print("\t - Encontrado TipoData.latLongAlt ! pointer= " + indexFound.ToString());
                            AnalizaLatLong(data, indexFound);
                            metricsFounds++;
                            break;

                        case (int)TipoData.ptchrollhdg:
                            if (DBG_PACKET_FND_OUT)
                                Debug.Print("\t - Encontrado TipoData.ptchrollhdg ! pointer= " + indexFound.ToString());
                            AnalizaPitchRoolHdg(data, indexFound);
                            metricsFounds++;
                            break;


                        case (int)TipoData.engineRpm:
                            if (DBG_PACKET_FND_OUT)
                                Debug.Print("\t - Encontrado TipoData.engineRpm ! pointer= " + indexFound.ToString());
                            AnalizaEngineRpm(data, indexFound);
                            metricsFounds++;
                            break;

                        case (int)TipoData.vspeed:
                            if (DBG_PACKET_FND_OUT)
                                Debug.Print("\t - Encontrado TipoData.vpeed ! pointer= " + indexFound.ToString());
                            AnalizaVSpeed(data, indexFound);
                            metricsFounds++;
                            break;

                        case (int)TipoData.slip:
                            if (DBG_PACKET_YAW_OUT)
                                Debug.Print("\t - Encontrado TipoData.slip/Yaw ! pointer= " + indexFound.ToString());
                            AnalizaSlipYaw(data, indexFound);
                            metricsFounds++;
                            break;

                        default:
                            break;
                    }
                    indexLastFound = 0;
                }

                indexDataHeaderList++;
            }

            if (metricsFounds > 0)
                qtdPacketsInterval++;

            if (metricsFounds >= DataHeaderList.Count -1)
                qtdPacketsIntervalOk++;
            else
                qtdPacketsIntervalEr++;

            Debug.Print("\t - Métricas Encontradas:  " + metricsFounds.ToString() + "/" + (DataHeaderList.Count - 1).ToString());


        }
        private static void AnalizaSlipYaw(byte[] data, int indexFound)
        {
            byte[] mybyteArray = new byte[4];
            float myVel1 = 0;
            float myVel2 = 0;
            float myVel3 = 0;
            float myVel4 = 0;
            float myVel8 = 0;

            Array.Copy(data, indexFound + 0, mybyteArray, 0, 4);
            myVel1 = System.BitConverter.ToSingle(mybyteArray, 0);  //  

            Array.Copy(data, indexFound + 4, mybyteArray, 0, 4);
            myVel2 = System.BitConverter.ToSingle(mybyteArray, 0);  //  

            Array.Copy(data, indexFound + 8, mybyteArray, 0, 4);
            myVel3 = System.BitConverter.ToSingle(mybyteArray, 0);  //        

            Array.Copy(data, indexFound + 12, mybyteArray, 0, 4);
            myVel4 = System.BitConverter.ToSingle(mybyteArray, 0);  // 

            Array.Copy(data, indexFound + 28, mybyteArray, 0, 4);
            myVel8 = System.BitConverter.ToSingle(mybyteArray, 0);  // 

            Airplane.Yaw = myVel8;

            if (DBG_PACKET_YAW_OUT)
                Debug.WriteLine("\t\t ( Slip/Yaw ) Veloc1 {0}, Veloc2 {1}, Veloc3 {2}, Veloc4 {3}, Veloc8 {4}", myVel1, myVel2, myVel3, myVel4, myVel8);

        }
        private static void AnalizaVSpeed(byte[] data, int indexFound)
        {
            byte[] mybyteArray = new byte[4];
            float myVel1 = 0;
            float myVel2 = 0;
            float myVel3 = 0;
            float myVel4 = 0;

            Array.Copy(data, indexFound + 0, mybyteArray, 0, 4);
            myVel1 = System.BitConverter.ToSingle(mybyteArray, 0);  //  

            Array.Copy(data, indexFound + 4, mybyteArray, 0, 4);
            myVel2 = System.BitConverter.ToSingle(mybyteArray, 0);  //  

            Array.Copy(data, indexFound + 8, mybyteArray, 0, 4);
            myVel3 = System.BitConverter.ToSingle(mybyteArray, 0);  //        

            Array.Copy(data, indexFound + 12, mybyteArray, 0, 4);
            myVel4 = System.BitConverter.ToSingle(mybyteArray, 0);  // 


            Airplane.VSpeed = myVel2;

            if (DBG_PACKET_VSP_OUT)
                Debug.WriteLine("\t\t ( Vertical Speed ) Veloc1 {0}, Veloc2 {1}, Veloc3 {2}, Veloc4 {3}", myVel1, myVel2, myVel3, myVel4);

        }
        private static void AnalizaEngineRpm(byte[] data, int indexFound)
        {
            byte[] mybyteArray = new byte[4];
            float myVel1;
            float myVel2;
            float myVel3;
            float myVel4;
            float myVel5;
            float myVel6;
            float myVel7;

            Array.Copy(data, indexFound + 0, mybyteArray, 0, 4);
            myVel1 = System.BitConverter.ToSingle(mybyteArray, 0);  //  

            Array.Copy(data, indexFound + 4, mybyteArray, 0, 4);
            myVel2 = System.BitConverter.ToSingle(mybyteArray, 0);  //  

            Array.Copy(data, indexFound + 8, mybyteArray, 0, 4);
            myVel3 = System.BitConverter.ToSingle(mybyteArray, 0);  //        

            Array.Copy(data, indexFound + 12, mybyteArray, 0, 4);
            myVel4 = System.BitConverter.ToSingle(mybyteArray, 0);  // 

            Array.Copy(data, indexFound + 16, mybyteArray, 0, 4);
            myVel5 = System.BitConverter.ToSingle(mybyteArray, 0); // 

            Array.Copy(data, indexFound + 20, mybyteArray, 0, 4);
            myVel6 = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 24, mybyteArray, 0, 4);
            myVel7 = System.BitConverter.ToSingle(mybyteArray, 0);

            Airplane.EngineRpm = myVel1;

            if (DBG_PACKET_RPM_OUT)
                Debug.WriteLine("\t\t ( Engine RPM ) Veloc1 {0}, Veloc2 {1}, Veloc3 {2}, Veloc4 {3}, Veloc5 {4}, Veloc6 {5}, Veloc7 {6}, Veloc8 {7}", myVel1, myVel2, myVel3, myVel4, myVel5, myVel6, myVel7);
        }
        private static void AnalizaPitchRoolHdg(byte[] data, int indexFound)
        {// E3 0C 45 3E 63 72 06 BD 1A 7C 7C 43 84 01 87 43 00 C0 79 C4 00 C0 79 C4 00 C0 79 C4 00 C0 79 C4
            byte[] mybyteArray = new byte[4];
            float myPrh1;
            float myPrh2;
            float myPrh3;
            float myPrh4;
            float myPrh5;
            float myPrh6;
            float myPrh7;
            float myPrh8;

            Array.Copy(data, indexFound + 0, mybyteArray, 0, 4);
            myPrh1 = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 4, mybyteArray, 0, 4);
            myPrh2 = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 8, mybyteArray, 0, 4);
            myPrh3 = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 12, mybyteArray, 0, 4);
            myPrh4 = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 16, mybyteArray, 0, 4);
            myPrh5 = System.BitConverter.ToSingle(mybyteArray, 0); // 

            Array.Copy(data, indexFound + 20, mybyteArray, 0, 4);
            myPrh6 = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 24, mybyteArray, 0, 4);
            myPrh7 = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 28, mybyteArray, 0, 4); // 
            myPrh8 = System.BitConverter.ToSingle(mybyteArray, 0);

            Airplane.Pitch = myPrh1;
            Airplane.Roll = myPrh2;
            Airplane.HeadingTrue = myPrh3;
            Airplane.HeadingMagn = myPrh4; /* todos em graus ... */



            if (DBG_PACKET_PRH_OUT)
                Debug.WriteLine("\t\t ( PitchRoolHdg ) Pitch {0}, Rool {1}, HdgTrue {2}, HdgMagn {3}", myPrh1, myPrh2, myPrh3, myPrh4);

        }
        private static void AnalizaVelocidades(byte[] data, int indexFound)
        {// 03 00 00 00 00 00 80 B8 32 27 C9 39 6F 50 D0 39 70 50 D0 39 00 C0 79 C4 BE 4C 93 B8 45 B9 EF 39 46 B9 EF 39
            byte[] mybyteArray = new byte[4];
            float myVel1;
            float myVel2;
            float myVel3;
            float myVel4;
            float myVel5;
            float myVel6;
            float myVel7;
            float myVel8;


            Array.Copy(data, indexFound + 0, mybyteArray, 0, 4);
            myVel1 = System.BitConverter.ToSingle(mybyteArray, 0);  // KIAS 

            Array.Copy(data, indexFound + 4, mybyteArray, 0, 4);
            myVel2 = System.BitConverter.ToSingle(mybyteArray, 0);  // KEAS 

            Array.Copy(data, indexFound + 8, mybyteArray, 0, 4);
            myVel3 = System.BitConverter.ToSingle(mybyteArray, 0);  // TAS       

            Array.Copy(data, indexFound + 12, mybyteArray, 0, 4);
            myVel4 = System.BitConverter.ToSingle(mybyteArray, 0);  // TGS

            Array.Copy(data, indexFound + 16, mybyteArray, 0, 4);
            myVel5 = System.BitConverter.ToSingle(mybyteArray, 0); // ESTRANHA

            Array.Copy(data, indexFound + 20, mybyteArray, 0, 4);
            myVel6 = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 24, mybyteArray, 0, 4);
            myVel7 = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 38, mybyteArray, 0, 4); // ESTRANHA
            myVel8 = System.BitConverter.ToSingle(mybyteArray, 0);

            Airplane.AirSpeed = myVel3;
            Airplane.GndSpeed = myVel4;
            Airplane.WndSpeed = 0;


            if (DBG_PACKET_SPD_OUT)
                Debug.WriteLine("\t\t ( Velocidades ) Veloc1 {0}, Veloc2 {1}, Veloc3 {2}, Veloc4 {3}, Veloc5 {4}, Veloc6 {5}, Veloc7 {6}, Veloc8 {7}", myVel1, myVel2, myVel3, myVel4, myVel5, myVel6, myVel7, myVel8);
        }
        private static void AnalizaLatLong(byte[] data, int indexFound)
        {
            byte[] mybyteArray = new byte[4];
            float myLat;
            float myLng;
            float myAlt;
            float myN1;
            float myN2;
            float myN3;
            float myN4;
            float myN5;

            Array.Copy(data, indexFound + 0, mybyteArray, 0, 4);
            myLat = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 4, mybyteArray, 0, 4);
            myLng = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 8, mybyteArray, 0, 4);
            myAlt = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 12, mybyteArray, 0, 4);
            myN1 = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 16, mybyteArray, 0, 4);
            myN2 = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 20, mybyteArray, 0, 4);
            myN3 = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 24, mybyteArray, 0, 4);
            myN4 = System.BitConverter.ToSingle(mybyteArray, 0);

            Array.Copy(data, indexFound + 28, mybyteArray, 0, 4);
            myN5 = System.BitConverter.ToSingle(mybyteArray, 0);

            if (DBG_PACKET_COO_OUT)
                Debug.WriteLine("\t\t ( LatLong ) Lat {0}, Long {1}, Alt {2}, N1 {3}, N2 {4}, N3 {5}, N4 {6}, N5 {7}", myLat, myLng, myAlt
                    , myN1, myN2, myN3, myN4, myN5);

            Airplane.CoordLat = myLat;
            Airplane.CoordLng = myLng;
            Airplane.Altitude = myAlt;

            Airplane.IsValid = true;
        }


    }

}
