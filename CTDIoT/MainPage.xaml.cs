using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Spi;
using Windows.Devices.Enumeration;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.Resources;
using Windows.Networking;
using Windows.Networking.Connectivity;
using System.Threading.Tasks;


// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace CTDIoT
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        int _counter = 0; // stevec, ki steje branja iz Raspberrija;
        int counter2 = 0; // pomozni stevec za povprecno temperaturo
        double temperatura = 0.0; // temperatura, ki bo prikazana na ekranu in spletni strani
        double stevec1 = 0.0; // stevec uporabljen za temperaturo
        double zacasniResultat = 0.0;
        double povprecnaTemperatura = 0.0;
        string opozorilo = " "; // alert na spletni strani od ConnectTheDots
        string sporocilo = " "; // sporocilo na spletni strani od ConnectTheDots
        string unit = "Celsius"; // Celsius ali Fahrenheit
        string imeNaprave = "Default"; 
        string lokacija = "Koper";
        double nivoAlarmaMin = 0; // minimalna temperaturo izpod katere se prikaze opozorilo Celsius
        double nivoAlarmaMax = 40; // minimalna temperaturo nad katero se prikaze opozorilo Celsius
        double nivoAlarmaMinF = 50; // minimalna temperaturo izpod katere se prikaze opozorilo Fahrenheit
        double nivoAlarmaMaxF = 100; // minimalna temperaturo nad katero se prikaze opozorilo Fahrenheit
        double stOsvezevanja = 5; // vsakih koliko sekund naj se prikaze in poslje podatek o temperaturi
        bool error = false; // error ce je napaka v vezju
        string ipRasp = "";
        //private const string EVENT_NAME = "rpievent";
        //private const string SECRET_KEY = "b7_7fraoWIp06b6RL6Qqnd";

        //generiranje GUID identifilacijkega imena za napravo
        Guid id = Guid.NewGuid();

        ConnectTheDotsHelper ctdHelper;
        DateTime date = DateTime.UtcNow.AddHours(23);

        public MainPage()
        {
            this.InitializeComponent();

            delay();
        }

        private async void delay()
        {
            InitSPI(); // inicializiramo Serial Peripheral Interface Bus
            //Debug.WriteLine("URA: {0}", DateTime.UtcNow);
            await Task.Delay(20000);
            //Debug.WriteLine("URA: {0}", DateTime.UtcNow);
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(0.1); // beremo iz Raspberrija vsakih 100 microsekun (0.1 ms)
            timer.Tick += Timer_Tick;

            ipRasp = GetCurrentIpv4Address();
            imeNaprave = ipRasp + "-" + imeNaprave;
            Inicializacija(unit, imeNaprave, lokacija);
            timer.Start();
            textBlock8.Text = "V poteku";
        }

        private async void InitSPI()
        {
            try
            {
                var settings = new SpiConnectionSettings(SPI_CHIP_SELECT_LINE);
                settings.ClockFrequency = 500000;// 10000000;
                settings.Mode = SpiMode.Mode0; //Mode3;

                var spiAqs = SpiDevice.GetDeviceSelector(SPI_CONTROLLER_NAME);
                var deviceInfo = await DeviceInformation.FindAllAsync(spiAqs);
                SpiDisplay = await SpiDevice.FromIdAsync(deviceInfo[0].Id, settings);
            }

            /* If initialization fails, display the exception and stop running */
            catch (Exception ex)
            {
                throw new Exception("SPI Initialization Failed", ex);
            }
        }

        //inicijalizacija in start programa *Ko kliknemo na Zacni in v startupu.
        public void Inicializacija(string unit, string imeNaprave, string lokacija)
        {
            // Hard coding guid for sensors. Not an issue for this particular application which is meant for testing and demos
            
            var sensors = new List<ConnectTheDotsSensor> {
                new ConnectTheDotsSensor(id.ToString(), "Temperature", unit),
            };

            ctdHelper = new ConnectTheDotsHelper(serviceBusNamespace: "actualw10iot-ns"/*, serviceBusNamespace: "actwiotns-ns"*/,
                eventHubName: "ehdevices",
                keyName: "RasPi",
                key: "hXK2vpF4WmcrCP/f+zbXUykE0M5c7R2w4UCWU+HkZbQ=",
                displayName: imeNaprave,
                organization: "Actual",
                location: lokacija,
                alerttype: opozorilo,
                message: sporocilo,
                sensorList: sensors);
        }

        private void Timer_Tick(object sender, object e)
        {
            var date2 = DateTime.UtcNow;
           
            if (date <= date2)
            {
                date = DateTime.UtcNow.AddHours(23);
                Inicializacija(unit, imeNaprave, lokacija);
            }
            DisplayTextBoxContents();
        }

        public void DisplayTextBoxContents()
        {
            
            SpiDisplay.TransferFullDuplex(writeBuffer, readBuffer);
            stevec1 = stevec1 + convertToDouble(readBuffer);
            _counter++;
            // preberemo nekaj vrednost. 
            // 317 branj = 5 sekund
            // Za optimizirati
            if (_counter == (225*(int)stOsvezevanja / 5))
            {
               temperatura= (stevec1 / _counter); // prikazana temperatura. Povprecje prebranih vrednosti. ce ne prevec skace temperatura.

                // ce ni napake na vezju.
                if (error == false)
                {
                    // pomozno za povprecno temperaturo
                    zacasniResultat = zacasniResultat + temperatura;
                    counter2++;

                    // povprecnje zadnjih 10 temperatur
                    if (counter2 == 10)
                    {
                        povprecnaTemperatura = (zacasniResultat / counter2); 
                        counter2 = 0;
                        zacasniResultat = 0;
                    }
                }
                
                // ce je ze izracunalo povprecno temperaturo in ce je prebrana temperatura prevec oddaljena od povprecne je napaka na vezju.
                if ((povprecnaTemperatura != 0.0) && ((temperatura <= povprecnaTemperatura - 10) || (temperatura >= povprecnaTemperatura + 10)))
                {
                    error = true; // napaka na vezju
                    textBlock10.Text = "Napaka na vezju! Senzor odstranjen?";
                }
                else
                {
                    error = false; // ok
                    textBlock10.Text = "";
                }
                
                if (unit == "Celsius")
                {
                    // ce je prebrana temperatura manjsa od nastavljenega nivoja alarma, poslje alarm
                    if (temperatura < nivoAlarmaMin)
                    {
                        if (error == true)
                        {
                            opozorilo = "Napaka na vezju! Senzor odstranjen? " + temperatura.ToString("#.#") + " \u00b0" + unit + " !!";
                            sporocilo = "Pregledati temperaturni senzor.";

                        }
                        else
                        {
                            opozorilo = "Pozor, nizka temperatura. " + temperatura.ToString("#.#") + " \u00b0" + unit + " !!";
                            sporocilo = "Nastaviti klimatsko napravo na " + ((nivoAlarmaMin + nivoAlarmaMax) / 2) + " \u00b0" + unit;
                        }    
                    }
                    // ce je prebrana temperatura vecja od nastavljenega nivoja alarma, poslje alarm
                    else if (temperatura > nivoAlarmaMax)
                    {
                        if (error == true)
                        {
                            opozorilo = "Napaka na vezju! Senzor odstranjen? " + temperatura.ToString("#.#") + " \u00b0" + unit + " !!";
                            sporocilo = "Pregledati temperaturni senzor.";

                        }
                        else
                        {
                            opozorilo = "Pozor, visoka temperatura. " + temperatura.ToString("#.#") + " \u00b0" + unit + " !!";
                            sporocilo = "Nastaviti klimatsko napravo na " + ((nivoAlarmaMin + nivoAlarmaMax) / 2) + " \u00b0" + unit;
                        }
                    }
                    // ni alarma
                    else
                    {
                        opozorilo = "";
                        sporocilo = "";
                    }
 
                    textPlaceHolder.Text = temperatura.ToString("#.#") + " \u00b0" + unit;
                    textPlaceHolder2.Text = povprecnaTemperatura.ToString("#.#") + " \u00b0" + unit;

                    //posljemo podatke na spletno stran in restartamo stevce in temperaturo
                    Poslji(null, null);
                    //if (temperatura < -20)
                        //SendIFTTTEvent(EVENT_NAME, "Pozor odstranjen senzor!");

                    _counter = 0;
                    temperatura= 0.0;
                    stevec1 = 0.0;
                }

                //Fahrenheit
                else
                {
                   temperatura= toFahrenheit(temperatura);
                    // ce je prebrana temperatura manjsa od nastavljenega nivoja alarma, poslje alarm
                    if (temperatura < nivoAlarmaMinF)
                    {
                        if (error == true)
                        {
                            opozorilo = "Napaka na vezju! Senzor odstranjen? " + temperatura.ToString("#.#") + " \u00b0" + unit + " !!";
                            sporocilo = "Pregledati temperaturni senzor.";

                        }
                        else
                        {
                            opozorilo = "Pozor, nizka temperatura. " + temperatura.ToString("#.#") + " \u00b0" + unit + " !!";
                            sporocilo = "Nastaviti klimatsko napravo na " + (int)((nivoAlarmaMinF + nivoAlarmaMaxF) / 2) + " \u00b0" + unit;
                        }
                    }
                    // ce je prebrana temperatura vecja od nastavljenega nivoja alarma, poslje alarm
                    else if (temperatura > nivoAlarmaMaxF)
                    {
                        if (error == true)
                        {
                            opozorilo = "Napaka na vezju! Senzor odstranjen? " + temperatura.ToString("#.#") + " \u00b0" + unit + " !!";
                            sporocilo = "Pregledati temperaturni senzor.";

                        }
                        else
                        {
                            opozorilo = "Pozor, visoka temperatura. " + temperatura.ToString("#.#") + " \u00b0" + unit + " !!";
                            sporocilo = "Nastaviti klimatsko napravo na " + (int)((nivoAlarmaMaxF + nivoAlarmaMinF) / 2) + " \u00b0" + unit;
                        }
                    }
                    // ni alarma
                    else
                    {
                        opozorilo = ""    ;
                        sporocilo = "";
                    }

                    textPlaceHolder.Text = temperatura.ToString("#.#") + " \u00b0" + unit;
                    if (povprecnaTemperatura != 0.0)
                    {
                        textPlaceHolder2.Text = toFahrenheit(povprecnaTemperatura).ToString("#.#") + " \u00b0" + unit;
                    }

                    //posljemo podatke na spletno stran in restartamo stevce in temperaturo
                    Poslji(null, null);
                    _counter = 0;
                    temperatura= 0.0;
                    stevec1 = 0.0;
                }
            }
        }

        public double convertToDouble(byte[] data)
        {
            /*Uncomment if you are using mcp3208/3008 which is 12 bits output */
            int result = data[1] & 0x0F;
            result <<= 8;
            result += data[2];
            double millivolts = Convert.ToDouble(result) * (3.4 / 4095); // (VOLTS / 4095) Volte prebrat z multimetrom.
            double temp_C = (millivolts - 0.5) * 100;
            return temp_C; //Dobimo Celsius
            //return millivolts;
            /*Uncomment if you are using mcp3002*/
            /* int result = data[1] & 0x03;
             result <<= 8;
             result += data[2];
             result =  (int)(result * (((3300.0 / 1024.0) - 100.0) / 10.0) - 40.0);
             return result ;*/
        }

        // posljemo podatke na spletno stran
        private void Poslji(object sender, RoutedEventArgs e)
        {
            ConnectTheDotsSensor sensor = ctdHelper.sensors.Find(item => item.measurename == "Temperature");
            sensor.value = temperatura;
            sensor.alerttype = opozorilo;
            sensor.message = sporocilo;
            ctdHelper.SendSensorData(sensor);
        }

        // Send IFTTT Event 
        /* private async void SendIFTTTEvent(string eventName, string value1)
         { 
             using (var client = new HttpClient()) 
             { 
                 // Send current luminosity value 
                 var values = new Dictionary<string, string>
                 { 
                    { "value1", value1 } 
                 }; 
 
 
                 var url = "https://maker.ifttt.com/trigger/" + eventName + "/with/key/" + SECRET_KEY; 
 
 
                 var content = new FormUrlEncodedContent(values); 
                 var response = await client.PostAsync(url, content); 
                 var responseString = await response.Content.ReadAsStringAsync(); 
             } 
         } */


        /*RaspBerry Pi2  Parameters*/
        private const string SPI_CONTROLLER_NAME = "SPI0";  /* For Raspberry Pi 2, use SPI0                             */
        private const Int32 SPI_CHIP_SELECT_LINE = 0;       /* Line 0 maps to physical pin number 24 on the Rpi2        */

        /*Uncomment if you are using mcp3208/3008 which is 12 bits output */
        byte[] readBuffer = new byte[3]; /*this is defined to hold the output data*/
        byte[] writeBuffer = new byte[3] { 0x06, 0x00, 0x00 };//00000110 00; // It is SPI port serial input pin, and is used to load channel configuration data into the device

        /*Uncomment if you are using mcp3002*/
        /* byte[] readBuffer = new byte[3]; /*this is defined to hold the output data*/
        // byte[] writeBuffer = new byte[3] { 0x68, 0x00, 0x00 };//01101000 00; /* It is SPI port serial input pin, and is used to load channel configuration data into the device*/

        private SpiDevice SpiDisplay;
        private DispatcherTimer timer; // Create a timer. 

        // Convert Celsius to Fahrenheit
        public double toFahrenheit(double temp)
        {
            double result = temp * 9/5 +32;
            return result;
        }

        //radio gumb za Celsius
        private void radioButton_Checked(object sender, RoutedEventArgs e)
        {
            unit = "Celsius";
        }

        //radio gumb za Fahrenheit
        private void radioButton1_Checked(object sender, RoutedEventArgs e)
        {
            unit = "Fahrenheit";
        }

        // ime naprave
        private void textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool temperatura= IsTextAllowed(textBox.Text);
            if (temperatura && (textBox.Text != ""))
            {
                imeNaprave = ipRasp + "-" + textBox.Text;
                textBlock9.Text = "";
            }
            else
            {
                imeNaprave = ipRasp + "-" + "Default";
                textBlock9.Text = "Samo znaki in številke!";
            }
        }

        //Lokacija
        private void textBox1_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool temperatura= String.IsNullOrEmpty(textBox1.Text);
            if (temperatura)
            {
                lokacija = "Default";
                textBlock9.Text = "Lokacija je obvezna. Prednastavljena lokacija: \"Default!\"";
            }
            else
            {
                 lokacija = textBox1.Text;
                textBlock9.Text = "";
            }
        }

        // Nivo alarma. Max
        private void textBox2_TextChanged(object sender, TextChangedEventArgs e)
        {
            double val = 0;
            bool temperatura= Double.TryParse(textBox2.Text, out val);

            if (temperatura == true && val > -40 && val < 120)
            {
                nivoAlarmaMax = val;
                nivoAlarmaMaxF = val;
                textBlock9.Text = "";
            }
            else
            {
                nivoAlarmaMax = 40;
                nivoAlarmaMaxF = 100;
                textBlock9.Text = "Meja za alarm je obvezna! Prednastavljena meja: \"40\"";
            }
        }

        // Nivo alarma. Min
        private void textBox3_TextChanged(object sender, TextChangedEventArgs e)
        {

            double val = 0;
           
            bool temperatura= Double.TryParse(textBox3.Text, out val);

            if (temperatura == true && val > -1 && val < 101)
            {
                nivoAlarmaMin = val;
                nivoAlarmaMinF = val;
                textBlock9.Text = "";
            }
            else
            {
                nivoAlarmaMin = 10;
                nivoAlarmaMin = 60;
                textBlock9.Text = "Meja za alarm je obvezna! Prednastavljena meja: \"10\"";
            }
        }

        //Osvezevanje
        private void textBox4_TextChanged(object sender, TextChangedEventArgs e)
        {


            double val = 0;
            bool temperatura= Double.TryParse(textBox4.Text, out val);

            if (temperatura == true && val > 4 && val < 7201)
            {
                stOsvezevanja = val;
                textBlock9.Text = "";
            }
            else
            {
                stOsvezevanja = 5;
                textBlock9.Text = "Hitrost osveževanja je obvezna! Prednastavljena hitrost: \"5s\"";
            }
        }

        // Gumb za start 
        private void button_Click(object sender, RoutedEventArgs e)
        {
            Inicializacija(unit, imeNaprave, lokacija);
            timer.Start();
            textBlock8.Text = "V poteku";
        }

        // Gumb za stop
        private void Button_Click2(object sender, RoutedEventArgs e)
        {
            timer.Stop();
            textBlock8.Text = "Ustavljeno";
        }

        //Funkcija za validacijo inputa
        private static bool IsTextAllowed(string text)
        {
            Regex regex = new Regex(@"^[a-zA-Z1-9]+$"); //regex that matches disallowed text
            return regex.IsMatch(text);
        }

        public static string GetCurrentIpv4Address()
        {
            var icp = NetworkInformation.GetInternetConnectionProfile();
            if (icp != null && icp.NetworkAdapter != null && icp.NetworkAdapter.NetworkAdapterId != null)
            {
                var name = icp.ProfileName;

                var hostnames = NetworkInformation.GetHostNames();

                foreach (var hn in hostnames)
                {
                    if (hn.IPInformation != null &&
                        hn.IPInformation.NetworkAdapter != null &&
                        hn.IPInformation.NetworkAdapter.NetworkAdapterId != null &&
                        hn.IPInformation.NetworkAdapter.NetworkAdapterId == icp.NetworkAdapter.NetworkAdapterId &&
                        hn.Type == HostNameType.Ipv4)
                    {
                        return hn.CanonicalName;
                    }
                }
            }

            var resourceLoader = ResourceLoader.GetForCurrentView();
            var msg = resourceLoader.GetString("NoInternetConnection");
            return msg;
        }

    }

    
}
