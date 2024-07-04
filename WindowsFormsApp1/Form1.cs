using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Speech.Recognition;
using System.Speech.AudioFormat;
using MediaToolkit;
using Microsoft.WindowsAPICodePack.Shell;
using MediaToolkit.Model;
using NAudio.Wave;
using Syn.Speech.Api;
using System.Net;
using System.Net.Sockets;
using System.Speech.Synthesis;
using System.Globalization;
using Google.Cloud.Speech.V1;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using SpeechRecognizer;
using CUETools.Codecs;
using CUETools.Codecs.FLAKE;
using Grpc.Auth;
using MediaToolkit.Options;
using WavSplitter;
using System.Diagnostics;
using Google.Cloud.Language.V1;
using Google.Protobuf.Collections;
using static Google.Cloud.Language.V1.AnnotateTextRequest.Types;
using Google.Cloud.Translation.V2;
using HtmlAgilityPack;
using System.Runtime.InteropServices;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        [DllImport("winmm.dll")]
        private static extern long mciSendString(string command, StringBuilder retstrign, int Returnlength, IntPtr callback);

        public Form1()
        {
            InitializeComponent();
            mciSendString("open new Type waveaudio alias recsound", null, 0, IntPtr.Zero);
            //button1.Click += new EventHandler(this.buttonClick);
        }

        OpenFileDialog fdlg;
        static bool completed;
        string transcript = "";
        SpeechRecognitionEngine recognizer = new SpeechRecognitionEngine();
        SpeechSynthesizer sre = new SpeechSynthesizer();
        int count = 1;
        string inputPath;
        string flacInput = @"C:\Users\Ahmad-PC\Documents\Visual Studio 2017\Projects\WindowsFormsApp1\temp.flac";
        string output;
        string language = "en";
        string story = null;
        string outputFolder = @"C:\Users\Ahmad-PC\Documents\Visual Studio 2017\Projects\WindowsFormsApp1\SplitStory\";
        int fileCount;
        string monoedWAV = @"C:\Users\Ahmad-PC\Documents\Visual Studio 2017\Projects\WindowsFormsApp1\monoed.wav";
        string outputPath = @"C:\Users\Ahmad-PC\Documents\Visual Studio 2017\Projects\WindowsFormsApp1\temp.wav";
        string jsonPath = @"C:\Users\Ahmad-PC\Documents\Visual Studio 2017\Projects\WindowsFormsApp1\Credentials.json";
        int splitCound = 0;
        string mode = "story";
        string[] filePaths;
        string EnglishSentence = "";
        List<string> nlpSentence = new List<string>();
        List<string> pslSentence = new List<string>();
        Font myfont = new Font("Times New Roman", 11.0f);
        bool fdlgCheck = false;
        //string[] sentences = new string[];
        private void button1_Click(object sender, EventArgs e)
        {
            fdlg = new OpenFileDialog();
            fdlg.Title = "Load Media file:";
            fdlg.InitialDirectory = @"C:\Users\Ahmad-PC\Documents\Visual Studio 2017\Projects\WindowsFormsApp1\DATA\English";
            fdlg.Filter = "All files (*.*)|*.*|All files (*.*)|*.*";
            fdlg.FilterIndex = 2;
            fdlg.RestoreDirectory = true;
            if (fdlg.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = fdlg.FileName;
            }
            textBox2.Clear();
            inputPath = fdlg.FileName;
            textBox2.Font = myfont;
            textBox2.AppendText("==========================\n");
            textBox2.AppendText("----------------Welcome---------------\n");
            textBox2.AppendText("==========================\n");
            fdlgCheck = true;
            
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            textBox3.Clear();
            transcript = "";
            textBox2.AppendText("==========================\n");
            textBox2.AppendText("----------------Welcome---------------\n");
            textBox2.AppendText("==========================\n");
            if (fdlgCheck )
            {
                if (Directory.Exists(outputFolder))
                {
                    Directory.Delete(outputFolder, true);
                    textBox2.AppendText("=> Directory Deleted\n");
                }
                textBox2.AppendText("=> Directory Created\n");
                Directory.CreateDirectory(outputFolder);
                
                convertFileToWave(inputPath, outputPath);
                textBox2.AppendText("=> File Converteed to .wav\n");
                StereoToMono(outputPath);
                SplitWave(monoedWAV, outputFolder, "split");
                getFilePaths(outputFolder);
                getDirCount(outputFolder);
                //initGrammer();
                SyncRecognizeWords(filePaths, 44100);
                if (language != "en")
                {
                    Translate(language, "en");
                    textBox3.Text = transcript;
                }
                else
                {
                    EnglishSentence = transcript;
                    textBox3.Text = transcript;
                }

            }
            else
                textBox2.AppendText("=> No file selected..\n");

            fdlgCheck = false;
        }

        void inputFromText(string path)
        {
            var logFile = File.ReadAllLines(path);
            nlpSentence.AddRange(logFile);
        }

        public void getFilePaths(string path)
        {
            filePaths = Directory.GetFiles(path);
        }

        void Translate(string sourceLang, string DesLang)
        {

            textBox2.AppendText("=> Translating...\n");
            TranslationClient client = TranslationClient.Create();
            var response = client.TranslateText(transcript, "en",
                sourceLang);
            EnglishSentence = response.TranslatedText;
            textBox2.AppendText(EnglishSentence+"\n");
            Console.WriteLine(response.TranslatedText);
            textBox3.AppendText("\n" + EnglishSentence);
        }


        string complexTOsimple(string complex)
        {
            string temp;
            temp = HTTP_POST("https://d5gate.ag5.mpi-sb.mpg.de/ClausIEGate/ClausIEGate", "inputtext=" + complex);
            textBox3.Clear();
            textBox3.AppendText(temp);
            return temp;
        }


        void SyncRecognizeWords(string[] filePath, int sampleRate)
        {

            textBox2.AppendText("==========================\n");
            textBox2.AppendText("=> Setting up Google Credentials...\n");
            GoogleCredential googleCredential;
            using (Stream m = new FileStream(jsonPath, FileMode.Open))
                googleCredential = GoogleCredential.FromStream(m);
            var channel = new Grpc.Core.Channel(SpeechClient.DefaultEndpoint.Host,
                googleCredential.ToChannelCredentials());
            textBox2.AppendText("=> Creating Google Speech Client..\n");
            var speech = SpeechClient.Create(channel);
            textBox2.AppendText("=> Google Speech Client Created..\n");

            //iterate all file paths and send each to google server
            int temp = 1;
            foreach (string s in filePaths)
            {
                textBox2.AppendText("=> Sending request " + temp.ToString() + " to server...\n");
                var response = speech.Recognize(new RecognitionConfig()
                {
                    Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                    SampleRateHertz = sampleRate,
                    LanguageCode = language,
                    EnableWordTimeOffsets = true,
                }, RecognitionAudio.FromFile(s));


                foreach (var result in response.Results)
                {
                    foreach (var alternative in result.Alternatives)
                    {

                        transcript = transcript + " " + alternative.Transcript;
                        //string temp2 = getPSLsentence(alternative.Transcript)+". ";

                        //story = story + temp2;

                        Console.WriteLine($"Transcript: { alternative.Transcript}");
                        Console.WriteLine("Word details:");
                        Console.WriteLine($" Word count:{alternative.Words.Count}");
                        foreach (var item in alternative.Words)
                        {
                            Console.WriteLine($"  {item.Word}");
                            Console.WriteLine($"    WordStartTime: {item.StartTime}");
                            Console.WriteLine($"    WordEndTime: {item.EndTime}");
                        }
                    }
                }

                temp++;
            }

            //MessageBox.Show(output,"Result");
            textBox2.AppendText("     ->Generating Transcript\n");
            textBox2.AppendText("     ->Transcript Saved\n");
            textBox2.AppendText("==========================\n");
            File.Delete(monoedWAV);


        }

        public string punctuateText(string s)
        {
            string output = HTTP_POST("http://bark.phon.ioc.ee/punctuator", "text=" + s);
            return output;
        }
        private void AnalyzeSyntaxFromText(string text)
        {
            nlpSentence.Clear();
            textBox2.AppendText("==========================\n");
            textBox2.AppendText("=> Setting up NLP Server...\n");
            GoogleCredential googleCredential;
            using (Stream m = new FileStream(jsonPath, FileMode.Open))
                googleCredential = GoogleCredential.FromStream(m);
            var channel = new Grpc.Core.Channel(LanguageServiceClient.DefaultEndpoint.Host,
                googleCredential.ToChannelCredentials());
            textBox2.AppendText("=> Creating Language Client..\n");
            var client = LanguageServiceClient.Create(channel);
            textBox2.AppendText("=> Language Client created..\n");
            var response = client.AnnotateText(new Document()
            {
                Content = text,
                Type = Document.Types.Type.PlainText
            },
            new Features() { ExtractSyntax = true });
            WriteSentences(response.Sentences, response.Tokens);
            textBox2.AppendText("=> Response Recieved..\n");
            textBox2.AppendText("==========================\n");
        }

        private void WriteSentences(IEnumerable<Sentence> sentences,
            RepeatedField<Token> tokens)
        {

            Console.WriteLine("Sentences:");
            foreach (var sentence in sentences)
            {
                nlpSentence.Add($"{sentence.Text.Content}");

                //textBox5.AppendText($"{sentence.Text.BeginOffset}: {sentence.Text.Content}\n");
            }
            Console.WriteLine("Tokens:");
            foreach (var token in tokens)
            {
                Console.WriteLine($"\t{token.PartOfSpeech.Tag} "
                    + $"{token.Text.Content}");
            }
        }
        private int SplitWave(string inputFile, string outputDirectory, string outputPrefix)
        {
            string sox = @"C:\Users\Ahmad-PC\Documents\Visual Studio 2017\Projects\WindowsFormsApp1\sox-14-4-2\sox.exe";

            int[] segments = { 30, 30, 30, 30, 30, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20 };

            IEnumerable<string> enumerable = segments.Select(s => "trim 0 " + s.ToString(CultureInfo.InvariantCulture));
            string @join = string.Join(" : newfile : ", enumerable);
            //fileCount=enumerable.Count();


            string cmdline = string.Format("\"{0}\" \"{1}%1n.wav" + "\" {2}", inputFile,
                    Path.Combine(outputDirectory, outputPrefix), @join);

            var processStartInfo = new ProcessStartInfo(sox, cmdline);
            Process start = System.Diagnostics.Process.Start(processStartInfo);
            start.WaitForExit();
            return 0;
        }

        int getDirCount(string path)
        {
            textBox2.AppendText("==========================\n");
            int count = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Count();
            textBox2.AppendText("=> File Count: " + count.ToString() + "\n");
            textBox2.AppendText("==========================\n");
            return count;
        }

        public int getSeconda()
        {
            string file = monoedWAV;
            ShellFile so = ShellFile.FromFilePath(file);
            double nanoseconds;
            double.TryParse(so.Properties.System.Media.Duration.Value.ToString(),
            out nanoseconds);
            Console.WriteLine("NanaoSeconds: {0}", nanoseconds);
            if (nanoseconds > 0)
            {
                double seconds = Convert100NanosecondsToMilliseconds(nanoseconds) / 1000;
                Console.WriteLine(seconds.ToString());
            }
            return 0;
        }

        public static double Convert100NanosecondsToMilliseconds(double nanoseconds)
        {
            // One million nanoseconds in 1 millisecond, 
            // but we are passing in 100ns units...
            return nanoseconds * 0.0001;
        }


        public string HTTP_POST(string Url, string Data)
        {
            string Out = String.Empty;
            System.Net.WebRequest req = System.Net.WebRequest.Create(Url);
            try
            {
                textBox2.AppendText("==========================\n");
                textBox2.AppendText("=> Setting Up Parameters..\n");
                req.Method = "POST";
                req.Timeout = 100000;
                req.ContentType = "application/x-www-form-urlencoded";
                byte[] sentData = Encoding.UTF8.GetBytes(Data);
                req.ContentLength = sentData.Length;
                using (System.IO.Stream sendStream = req.GetRequestStream())
                {
                    sendStream.Write(sentData, 0, sentData.Length);
                    sendStream.Close();
                }
                textBox2.AppendText("=> Writing to Server..\n");
                System.Net.WebResponse res = req.GetResponse();
                System.IO.Stream ReceiveStream = res.GetResponseStream();
                textBox2.AppendText("=> Response Recieved..\n");
                textBox2.AppendText("==========================\n");
                using (System.IO.StreamReader sr = new System.IO.StreamReader(ReceiveStream, Encoding.UTF8))
                {
                    Char[] read = new Char[256];
                    int count = sr.Read(read, 0, 256);

                    while (count > 0)
                    {
                        String str = new String(read, 0, count);
                        Out += str;
                        count = sr.Read(read, 0, 256);
                    }
                }
            }
            catch (ArgumentException ex)
            {
                Out = string.Format("HTTP_ERROR :: The second HttpWebRequest object has raised an Argument Exception as 'Connection' Property is set to 'Close' :: {0}", ex.Message);
            }
            catch (WebException ex)
            {
                Out = string.Format("HTTP_ERROR :: WebException raised! :: {0}", ex.Message);
            }
            catch (Exception ex)
            {
                Out = string.Format("HTTP_ERROR :: Exception raised! :: {0}", ex.Message);
            }

            return Out;
        }
        void jnlpExec(string path)
        {
            var javaws = File.Exists(@"C:\Program Files\Java\jre1.8.0_162\bin\javaws.exe") ? @"C:\Program Files\Java\jre1.8.0_162\bin\javaws.exe" : @"C:\Program Files (x86)\Java\jre1.8.0_161\bin\javaws.exe";
            var psi = new ProcessStartInfo(javaws, String.Format(path));
            //C: \Users\Ahmad - PC\Desktop\Project\JASApp - 20180323T111213Z - 001\JASApp\SiGMLServiceClientApp.jnlp
            // make sure child process is already started
            Process.Start(psi);

            //foreach (Process p in Process.GetProcessesByName("javaw"))
            //{
            //    p.WaitForExit();
            //}
        }

        private void initGrammer()
        {
            recognizer.Dispose();
            try
            {
                textBox2.Clear();
                transcript = "";
                var culture = new CultureInfo("en-US");
                recognizer = new SpeechRecognitionEngine(culture);
                foreach (string s in filePaths)
                {
                    recognizer.SetInputToWaveFile(s);

                    Grammar dictation = new DictationGrammar();
                    dictation.Name = "Dictation Grammar";

                    recognizer.LoadGrammar(dictation);
                    //recognizer.LoadGrammar(GetGrammer());

                    recognizer.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(recognizer_SpeechRecognized);

                    recognizer.RecognizeAsync(RecognizeMode.Multiple);

                    sre.SelectVoiceByHints(VoiceGender.Male, VoiceAge.Child);
                    sre.Rate = -2;
                }


            }
            catch (Exception ex)
            {
                textBox2.Text = ex.InnerException.Message;
            }
        }

        private Grammar GetGrammer()
        {

            var choices = new Choices();
            //add custom commands
            choices.Add(File.ReadAllLines(@"C:\Users\Ahmad-PC\Documents\Visual Studio 2017\Projects\WindowsFormsApp1\Commands.txt"));
            //to add the letters to the dictionary
            choices.Add(Enum.GetNames(typeof(Keys)).ToArray());

            var grammer = new Grammar(new GrammarBuilder(choices));
            textBox2.Text = "---Grammer loaded---";
            return grammer;
        }
        private void label1_Click(object sender, EventArgs e)
        {

        }

        // Handle the SpeechRecognized event.
        void recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result != null && e.Result.Text != null)
            {
                transcript += e.Result.Text;
                textBox3.AppendText(e.Result.Text);
            }
            else
            {
                textBox3.Text = "Recognized text not available";
            }
        }

        // Handle the RecognizeCompleted event.
        void recognizer_RecognizeCompleted(object sender, RecognizeCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                textBox3.Text = "  Error encountered: " +
                e.Error.GetType().Name + " : " + e.Error.Message;
            }
            if (e.Cancelled)
            {
                textBox3.Text = "  Operation cancelled.";
            }
            if (e.InputStreamEnded)
            {
                textBox3.Text = "  End of stream encountered.";
            }
            completed = true;
        }

        void printMetadata(string path)
        {
            var inputFile = new MediaFile { Filename = path };

            using (var engine = new Engine())
            {
                engine.GetMetadata(inputFile);
            }
            using (StringWriter stringWriter = new StringWriter())
            {
                Console.SetOut(stringWriter);
                Console.Write(inputFile.Metadata.AudioData);
                string allConsoleOutput = stringWriter.ToString();
                textBox3.Text = allConsoleOutput;
            }
        }
        void convertFileToWave(string input, string output)
        {
            var inputFile = new MediaFile { Filename = input };
            var outputFile = new MediaFile { Filename = output };

            var conversionOptions = new ConversionOptions
            {
                AudioSampleRate = AudioSampleRate.Hz44100
            };

            using (var engine = new Engine())
            {
                engine.Convert(inputFile, outputFile, conversionOptions);
            }

            using (var engine = new Engine())
            {
                Console.WriteLine(engine.GetType());
                engine.GetMetadata(outputFile);
            }
            textBox2.AppendText("==========================\n");
            textBox2.AppendText("=> Conversion Completed\n");
            textBox2.AppendText("=> Duration : " + outputFile.Metadata.Duration.ToString() + "\n");
            textBox2.AppendText("     ->Input sample rate : " + inputFile.Metadata.AudioData.SampleRate + "\n");
            textBox2.AppendText("     ->Output sample rate: " + outputFile.Metadata.AudioData.SampleRate + "\n");
            textBox2.AppendText("     ->Input Channel output: " + inputFile.Metadata.AudioData.ChannelOutput + "\n");
            textBox2.AppendText("     ->Output sample rate: " + outputFile.Metadata.AudioData.ChannelOutput + "\n");
            textBox2.AppendText("==========================\n");
        }



        public void StereoToMono(string sourceFile)
        {
            WaveFormat target = new WaveFormat(44100, 16, 1);
            WaveStream stream = new WaveFileReader(sourceFile);
            WaveFormatConversionStream str = new WaveFormatConversionStream(target, stream);
            WaveFileWriter.CreateWaveFile(monoedWAV, str);
            stream.Close();
        }

        private void axWindowsMediaPlayer1_Enter(object sender, EventArgs e)
        {

        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            axWindowsMediaPlayer1.URL = inputPath;
        }

        public WaveIn waveSource = null;
        public WaveFileWriter waveFile = null;
        private void button4_Click(object sender, EventArgs e)
        {
            axWindowsMediaPlayer1.Ctlcontrols.stop();
            waveSource.StopRecording();
            fdlgCheck = true;
            inputPath = outputPath;
            textBox2.AppendText("Recording Stopped...\n");
        }


        string getPSLsentence(string s, int i)
        {
            //textBox2.AppendText("==========================\n");

            IPEndPoint serverAddress = new IPEndPoint(IPAddress.Parse("127.0.0.1"/*"192.168.43.178"*/), 7183);
            textBox2.AppendText("          <<< " + i + " Sentences Left >>>\n");
            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Connect(serverAddress);


            string toSend = s;
            //textBox2.AppendText("=> To send : " + toSend + "\n");
            // Sending
            int toSendLen = System.Text.Encoding.ASCII.GetByteCount(toSend);
            byte[] toSendBytes = System.Text.Encoding.ASCII.GetBytes(toSend);
            byte[] toSendLenBytes = System.BitConverter.GetBytes(toSendLen);
            clientSocket.Send(toSendLenBytes);
            clientSocket.Send(toSendBytes);


            // Receiving
            byte[] rcvLenBytes = new byte[4];
            clientSocket.Receive(rcvLenBytes);
            int rcvLen = System.BitConverter.ToInt32(rcvLenBytes, 0);
            byte[] rcvBytes = new byte[rcvLen];
            clientSocket.Receive(rcvBytes);
            String rcv = System.Text.Encoding.ASCII.GetString(rcvBytes);
            if (rcv != null)
            {
                pslSentence.Add(rcv);
                //textBox6.AppendText( rcv + "\n");
                return rcv;
            }
            else
                textBox2.Text = "=> Unable...";
            Console.WriteLine("Client received: " + rcv);

            return "";
            clientSocket.Close();

            //textBox2.AppendText("==========================\n");
        }



        public void button5_Click(object sender, EventArgs e)
        {

            int countt = 1;
            textBox2.AppendText("==========================\n");
            pslSentence.Clear();
            textBox6.Clear();
            int temp = nlpSentence.Count;
            textBox2.AppendText("=>        Connected to JASAPP..\n");
            textBox2.AppendText("       <<<Request Sent to server>>>\n");
            foreach (var n in nlpSentence)
            {
                getPSLsentence(n.ToString(), temp);
                temp--;
            }
            textBox2.AppendText("       >>>Request Recieved<<<\n");
            foreach (var s in pslSentence)
            {
               textBox6.AppendText(s.ToString());
          
            }
            textBox2.AppendText("==========================\n");
            System.IO.File.WriteAllLines(@"C:\Users\Ahmad-PC\Documents\Visual Studio 2017\Projects\WindowsFormsApp1\OutputSenList.txt", pslSentence);
            //using (TextWriter tw = new StreamWriter("OutputSenList.txt"))
            //{
            //    foreach (String s in pslSentence)
            //        tw.WriteLine(s);
            //}
        }

        private void button6_Click(object sender, EventArgs e)
        {
            transcript = textBox3.Text;
            EnglishSentence = transcript;
            if (mode == "Sentence")
                nlpSentence.Add(EnglishSentence);
        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox5_TextChanged(object sender, EventArgs e)
        {

        }

        private void button7_Click(object sender, EventArgs e)
        {
            if (language == "en") {
                textBox4.Clear();
                story = story + "\n";
                //Translate("en","en");
                story = punctuateText(EnglishSentence);
            }
            else if (language=="ur") {
                story = EnglishSentence;
            }
            textBox4.Text = story;
        }

        private void button8_Click(object sender, EventArgs e)
        {
            textBox5.Clear();
            int count = 1;
            if (story == null && transcript != null)
            {
                story = transcript;
            }
            foreach (string s in nlpSentence)
            {
                Console.WriteLine(count.ToString() + ": " + s);
                textBox2.AppendText(count.ToString() + ": " + s + "\n");
                count++;
            }
            count = 1;
            AnalyzeSyntaxFromText(story);
            foreach (string s in nlpSentence)
            {
                Console.WriteLine(count.ToString() + ": " + s);
                textBox5.AppendText(count.ToString() + ": " + s + "\n");
                count++;
            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                language = "en";
            }
            else
                language = "en";
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked)
            {
                language = "ur";
            }
            else
                language = "en";
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked)
            {
                mode = "sentence";
            }
            else
                mode = "story";
        }

        private void button9_Click(object sender, EventArgs e)
        {
            inputFromText(fdlg.FileName);
            AnalyzeSyntaxFromText(nlpSentence.First());
            textBox5.Clear();
            foreach (string s in nlpSentence)
            {
                textBox5.AppendText(s + "\n");
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            jnlpExec(@"C:\Users\Ahmad-PC\Desktop\Project\JASApp-20180323T111213Z-001\JASApp\SiGMLServiceClientApp.jnlp");
            jnlpExec(@"C:\Users\Ahmad-PC\Desktop\Project\JASApp-20180323T111213Z-001\JASApp\SiGMLServicePlayer-plus.jnlp");
        }

        private void button11_Click(object sender, EventArgs e)
        {
            string html = complexTOsimple(EnglishSentence);
            
        }

        private void button12_Click(object sender, EventArgs e)
        {
            waveSource = new WaveIn();
            waveSource.WaveFormat = new WaveFormat(44100, 1);

            waveSource.DataAvailable += new EventHandler<WaveInEventArgs>(waveSource_DataAvailable);
            waveSource.RecordingStopped += new EventHandler<StoppedEventArgs>(waveSource_RecordingStopped);

            waveFile = new WaveFileWriter(@"C:\Users\Ahmad-PC\Documents\Visual Studio 2017\Projects\WindowsFormsApp1\temp.wav", waveSource.WaveFormat);
            textBox2.AppendText("Recording Started...\n");
            waveSource.StartRecording();
        }

        void waveSource_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (waveFile != null)
            {
                waveFile.Write(e.Buffer, 0, e.BytesRecorded);
                waveFile.Flush();
            }
        }

        void waveSource_RecordingStopped(object sender, StoppedEventArgs e)
        {
            if (waveSource != null)
            {
                waveSource.Dispose();
                waveSource = null;
            }

            if (waveFile != null)
            {
                waveFile.Dispose();
                waveFile = null;
            }
        }
    }
}

    