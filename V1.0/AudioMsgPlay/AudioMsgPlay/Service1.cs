using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AudioMsgPlay
{
    public partial class Service1 : ServiceBase
    {
        MySqlConnection con = null;
        public static StreamReader STR;
        public static StreamWriter STW;
        public static String DbPath = "", TimerCallSec = "";
        public static Boolean DebugLog = false;
        public static String logsPath = "";
        public static String ErrorLogsPath = "";
        public string ServiceDir = AppDomain.CurrentDomain.BaseDirectory;
        SpeechSynthesizer myspeaker = new SpeechSynthesizer();
        private System.Timers.Timer timer;

        public Service1()
        {
            InitializeComponent();
            Boolean result = ReadFile();
        }

        protected override void OnStart(string[] args)
        {
            WriteToLogFile("Service is started at " + DateTime.Now);
            // this.timer = new System.Timers.Timer(30000D);  // 30000 milliseconds = 30 seconds
            this.timer = new System.Timers.Timer(Int32.Parse(TimerCallSec) * 1000D);  // Dynamic Time Seconds
            this.timer.AutoReset = true;
            this.timer.Elapsed += new System.Timers.ElapsedEventHandler(this.timer_Elapsed);
            this.timer.Start();
        }

        protected override void OnStop()
        {
            WriteToLogFile("Service is stopped at " + DateTime.Now);
            this.timer.Stop();
            this.timer = null;
        }

        /***************************WriteToLogFile*******************************/
        public void WriteToLogFile(string Message)
        {
            if (!File.Exists(ServiceDir + "\\ConnectivityLogs.txt"))
            {
                // Create a file to write to.   
                StreamWriter sw1 = File.CreateText(ServiceDir + "\\ConnectivityLogs.txt");

                sw1.WriteLine(Message);
                sw1.Close();
            }
            else
            {
                try
                {
                    StreamWriter sw1 = File.AppendText(ServiceDir + "\\ConnectivityLogs.txt");

                    sw1.WriteLine(Message);
                    sw1.Close();
                }
                catch (Exception ex)
                {
                    // Console.WriteLine(ex);
                }
            }
        }

        /***************************WriteToLogFile*******************************/
        public void WriteToErrorFile(string Message)
        {
            if (!File.Exists(ServiceDir + "\\ErrorLogs.txt"))
            {
                // Create a file to write to.   
                StreamWriter sw1 = File.CreateText(ServiceDir + "\\ErrorLogs.txt");

                sw1.WriteLine(Message);
                sw1.Close();
            }
            else
            {
                try
                {
                    StreamWriter sw1 = File.AppendText(ServiceDir + "\\ErrorLogs.txt");

                    sw1.WriteLine(Message);
                    sw1.Close();
                }
                catch (Exception ex)
                {
                    // Console.WriteLine(ex);
                }
            }
        }

        /***************************Read Text File*******************************/
        public Boolean ReadFile()
        {
            try
            {
                int count = 0;
                String line = null;
                String file = ServiceDir + "\\config.txt";

                logsPath = ServiceDir + "\\ConnectivityLogs.txt";
                ErrorLogsPath = ServiceDir + "\\ErrorLogs.txt";

                StreamReader myfile = new StreamReader(file);

                while (((line = myfile.ReadLine()) != null) || (count < 3))
                {
                    if (count == 0)
                    {
                        DbPath = line;
                        DbPath = DbPath.Replace("DbPath=", "");
                    }
                    if (count == 1)
                    {
                        TimerCallSec = line;
                        TimerCallSec = TimerCallSec.Replace("TimerCallSec=", "");
                    }
                    else if (count == 2)
                    {
                        String Log = line;
                        Log = Log.Replace("DebugLog=", "");

                        if (Log.Equals("True"))
                        {
                            DebugLog = true;
                        }
                        else
                        {
                            DebugLog = false;
                        }
                    }
                    count++;
                }

                myfile.Close();
                return true;
            }
            catch (Exception ex)
            {
                if (DebugLog == true)
                {
                    WriteToErrorFile("Exception in ReadFile: " + DateTime.Now + "  " + ex.Message.ToString());
                }
                return false;
            }
        }

        private void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (DebugLog == true)
            {
                WriteToLogFile("Finding New Message: " + DateTime.Now);
            }
            read_and_play_New_Msg();
        }

        public void read_and_play_New_Msg()
        {
            String readVehicleNo = "", readNewMessage = "";

            try
            {
                con = new MySqlConnection(DbPath.ToString());
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT vehiclenumber, msg FROM audiomsg WHERE actionplay = 'New'";
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    // MessageBox.Show(Reader.GetValue(0).ToString());
                    readVehicleNo = Reader["vehiclenumber"].ToString();
                    readNewMessage = Reader["msg"].ToString();
                }
                if (!string.IsNullOrEmpty(readVehicleNo) && !string.IsNullOrEmpty(readNewMessage))
                {
                    myspeaker.SpeakAsync(readNewMessage);
                    if (DebugLog == true)
                    {
                        WriteToLogFile("New Message: " + readNewMessage + " of Vehicle Number: " + readVehicleNo + " - " + DateTime.Now);
                    }
                }
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                if (DebugLog == true)
                {
                    WriteToErrorFile("New Message Not Found: " + ex + " - " + DateTime.Now);
                }
            }

            try
            {
                string actionPlay = "Old";
                string Query = "UPDATE audiomsg SET actionplay= '" + actionPlay + "' WHERE id= '1'";
                con = new MySqlConnection(DbPath.ToString());
                MySqlCommand MyCommand2 = new MySqlCommand(Query, con);
                MySqlDataReader MyReader2;
                con.Open();
                MyReader2 = MyCommand2.ExecuteReader();
                con.Close();//Connection closed here  
            }
            catch (Exception ex)
            {
                con.Close();
                if (DebugLog == true)
                {
                    WriteToErrorFile("Message Not Updated: " + ex + " - " + DateTime.Now);
                }
            }

        }
    }
}
