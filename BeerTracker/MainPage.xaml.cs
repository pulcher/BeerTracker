using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;
using Windows.Devices.Spi;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BeerTracker
{
    public sealed partial class MainPage : Page
    {
        private GpioController _gpio;
        private I2cDevice _adc;
        private SpiDevice SpiADC;
        private DispatcherTimer timer;
        private Timer periodicTimer;
        private DeviceInformationCollection _devices;

        public MainPage()
        {
            this.InitializeComponent();
            Unloaded += MainPage_Unloaded;

            InitAll();
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _adc.Dispose();
        }

        private async void InitAll()
        {
            try
            {
                InitGPIO();         /* Initialize GPIO to toggle the LED                          */
                //await InitSPI();    /* Initialize the SPI bus for communicating with the ADC      */
                await InitI2C();        // Initialize the I2C bus

            }
            catch (Exception ex)
            {
                I2CStatus.Text = ex.Message;
                return;
            }

            ///* Now that everything is initialized, create a timer so we read data every 100mS */
            periodicTimer = new Timer(this.PeriodTimer_Tick, null, 0, 500);

            //if (buttonPin != null)
            //    timer.Start();

            I2CStatus.Text += " Blink running";
        }

        private void PeriodTimer_Tick(object state)
        {
            ReadADC();
            var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                MessageJunk.Text = string.Format("name: {0}, IsEnabled: {1}, Prop: {2}",
                    _devices[0].Name, _devices[0].IsEnabled, string.Join(", ", _devices[0].Properties.Values));
            });
        }

        private void InitGPIO()
        {
            _gpio = GpioController.GetDefault();
            var inGpioPin = _gpio.OpenPin(27);

            inGpioPin.ValueChanged += ConversionReady;
        }

        private void ConversionReady(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            ReadADC();
        }

        private async Task InitI2C()
        {
            try
            {
                var i2CSettings = new I2cConnectionSettings(0x48)
                {
                    BusSpeed = I2cBusSpeed.FastMode,
                    SharingMode = I2cSharingMode.Shared
                };

                var i2C1 = I2cDevice.GetDeviceSelector("I2C1");

                _devices = await DeviceInformation.FindAllAsync(i2C1);

                _adc = await I2cDevice.FromIdAsync(_devices[0].Id, i2CSettings);
            }
            catch (Exception ex)
            {
                throw new Exception("I2C Initialization Failed", ex);
            }

            _adc.Write(new byte[] { 0x01, 0xc2, 0x20 });
            _adc.Write(new byte[] { 0x02, 0x00, 0x00 });
            _adc.Write(new byte[] { 0x03, 0xff, 0xff });
        }

        public void ReadADC()
        {
            if (_adc != null)
            {
                var bytearray = new byte[2];
                _adc.WriteRead(new byte[] { 0x0 }, bytearray);

                // Convert to int16
                // Converti en int16
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytearray);

                var adcValue = BitConverter.ToInt16(bytearray, 0);

                /* UI updates must be invoked on the UI thread */
                var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    PotSetting.Text = adcValue.ToString();         /* Display the value on screen                      */
                });
            }
            else
            {
                var task2 = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    MessageJunk.Text = "_adc is null";
                });
            }
        }

        private void exitButton_Click(object sender, RoutedEventArgs e)
        {
            if (_adc != null)
            {
                _adc.Dispose();
                _adc = null;
            }

            InitI2C();
        }
    }
}
