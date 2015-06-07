using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.DirectoryServices;
using System.Security.Cryptography;
using MySql.Data.MySqlClient;


namespace WindowsFormsApplication1
{
    public partial class Form1 : Form
    {
        private const string _adIp = "10.13.37.2";
        private const string _eventId = "6";
        private const int _IntervalTime = 300; // in seconds
        private const string _userName = "Administrator@multimediajugend.de";
        private const string _passWord = "*****";

        private int cnt = 0;

        public Form1()
        {
            InitializeComponent();
        }

        private void getUsers()
        {
            string myConnectionString = "SERVER=10.13.37.5;" +
                            "DATABASE=database;" +
                            "UID=username;" +
                            "PASSWORD=password;";

            MySqlConnection connection = new MySqlConnection(myConnectionString);
            MySqlCommand command = connection.CreateCommand();
            //command.CommandText = "SELECT t1.userid, t1.email, t1.password, t1.nickname, t1.prename, t1.lastname FROM hfh_users as t1,hfh_register as t2 WHERE t2.eventid=" + eventId + " AND t2.userid=t1.userid ";
            command.CommandText = "SELECT DISTINCT t1.userid, t1.email, t1.password, t1.nickname, t1.prename, t1.lastname FROM hfh_users AS t1, hfh_register AS t2, hfh_group_users AS t3 WHERE (t2.eventid = " + _eventId + " AND t2.userid = t1.userid) OR t1.userid = t3.userid";
            
            MySqlDataReader Reader;
            connection.Open();
            Reader = command.ExecuteReader();
            while (Reader.Read())
            {
                string row = "";
                for (int i = 0; i < Reader.FieldCount; i++)
                    row += Reader.GetValue(i).ToString() + ", ";
                Console.WriteLine(row);
                //if(Reader.GetValue(0).ToString()=="9")
                CreateUserAccount(String.Format("{0}/CN=Users,DC=multimediajugend,DC=de", _adIp),
                                  Reader.GetValue(1).ToString(),
                                  Reader.GetValue(2).ToString(),
                                  Reader.GetValue(4).ToString(),
                                  Reader.GetValue(5).ToString(),
                                  Reader.GetValue(0).ToString(),
                                  Reader.GetValue(1).ToString());
            }
            connection.Close();

        }

        private void InsertAllUsers()
        {
            progressBar1.Style = ProgressBarStyle.Marquee;
            backgroundWorker1.RunWorkerAsync();
        }

        public string CreateUserAccount(string ldapPath, string userName, string userPassword, string givenName, string lastName, string id, string email)
        {
            string oGUID = string.Empty;

            if (ldapPath != "" && userName != "" && userPassword != "" && givenName != "" && lastName != "" && id != "" && email != "")
            {
                try
                {
                    //rewrite email to username: 
                    userName = userName.Replace('@', '.');

                    string connectionPrefix = "LDAP://" + ldapPath;
                    DirectoryEntry dirEntry = new DirectoryEntry(connectionPrefix, _userName, _passWord);
                    DirectoryEntry newUser = dirEntry.Children.Add
                        ("CN=" + userName, "user");
                    //newUser.Properties["samAccountName"].Value = "testalt";
                    newUser.Properties["userPrincipalName"].Value = userName + "@multimediajugend.de";
                    newUser.Properties["givenName"].Value = givenName;
                    newUser.Properties["initials"].Value = id;
                    newUser.Properties["sn"].Value = lastName;
                    newUser.Properties["mail"].Value = email;

                    newUser.CommitChanges();
                    oGUID = newUser.Guid.ToString();

                    newUser.Invoke("SetPassword", new object[] { userPassword });
                    newUser.CommitChanges();
                    dirEntry.Close();
                    newUser.Close();

                    Enable(userName);
                }
                catch (System.DirectoryServices.DirectoryServicesCOMException E)
                {
                    if (E.ExtendedError == 8305)
                        ResetPassword(userName, userPassword);
                    else
                        MessageBox.Show(E.Message.ToString()); //ExtendedDN 8305
                }
                //MessageBox.Show(oGUID);
            }
            return oGUID;
        }

        public void ResetPassword(string userDn, string password)
        {
            DirectoryEntry uEntry = new DirectoryEntry(String.Format("LDAP://{0}/CN={1},CN=Users,DC=multimediajugend,DC=de", _adIp, userDn),
                                                       _userName,
                                                       _passWord);
            uEntry.Invoke("SetPassword", new object[] { password }); //Tilli: Bei Exception: InnerException anguggen
            uEntry.Properties["LockOutTime"].Value = 0; //unlock account

            uEntry.Close();
        }

        public void Enable(string userDn)
        {
            try
            {
                DirectoryEntry user = new DirectoryEntry(String.Format( "LDAP://{0}/CN={1},CN=Users,DC=multimediajugend,DC=de", _adIp, userDn),
                                                         _userName,
                                                         _passWord);
                int val = (int)user.Properties["userAccountControl"].Value;
                user.Properties["userAccountControl"].Value = val & ~0x2;
                //ADS_UF_NORMAL_ACCOUNT;

                user.CommitChanges();
                user.Close();
            }
            catch (System.DirectoryServices.DirectoryServicesCOMException E)
            {
                //DoSomethingWith --> E.Message.ToString();
                MessageBox.Show(E.Message.ToString());
            }
        }



        static string getMd5Hash(string strInput)
        {
            MD5 md5Hasher = MD5.Create();
            return BitConverter.ToString(md5Hasher.ComputeHash(Encoding.Default.GetBytes(strInput))).Replace("-", "");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            StartUpdate();
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            getUsers();
            //progressBar1.Invoke(setContinous);
        }
        private void setContinous()
        {
            progressBar1.Style = ProgressBarStyle.Continuous;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            progressBar1.Style = ProgressBarStyle.Blocks;
            cnt = _IntervalTime;
            labelStatus.Text = String.Format("Refresh in: {0:D2}:{1:D2}", cnt / 60, cnt % 60);

            timerSec.Start();
            button3.Enabled = true;
        }

        private void timerSec_Tick(object sender, EventArgs e)
        {
            cnt--;
            labelStatus.Text = String.Format("Refresh in: {0:D2}:{1:D2}", cnt / 60, cnt % 60);
            if (cnt <= 0)
            {
                StartUpdate();
            }
        }

        private void StartUpdate()
        {
            labelStatus.Text = "Inserting users!";
            timerSec.Stop();
            InsertAllUsers();
        }
    }
}
