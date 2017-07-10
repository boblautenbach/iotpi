using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
using Windows.Data.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Dynamic;

namespace iotpi
{
    public sealed partial class MainPage : Page
    {
        // Speech Recognizer
        private SpeechRecognizer recognizer;

        private HttpClient client = new HttpClient();
        private readonly string baseURL = "https://api.api.ai/api/query?v=20150910&lang=en";
        private readonly string APIKEY = "092e4a9c21d34bed9e4f50e7ebf56d2d";

        private readonly string INVOCATION_NAME = "hal";
        private readonly string INVOCATION_PROMPT = "yes?";

        public MainPage()
        {
            this.InitializeComponent();
        }

        async protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", APIKEY);
            await InitializeSpeechRecognizer();
        }
       
        // Release resources, stop recognizer, release pins, etc...
        private async void MainPage_Unloaded(object sender, object args)
        {
            // Stop recognizing
            await recognizer.ContinuousRecognitionSession.StopAsync();
        }

        private async Task InitializeIntentRecognizer()
        {
            string spokenWord = string.Empty;

            try
            {
                // Initialize recognizer
                using (var intentRecognizer = new SpeechRecognizer())
                {
                    var compilationResult = await intentRecognizer.CompileConstraintsAsync();
                    // If successful, display the recognition result.
                    if (compilationResult.Status == SpeechRecognitionResultStatus.Success)
                    {
                        // change default of 5 seconds
                        intentRecognizer.Timeouts.InitialSilenceTimeout = TimeSpan.FromSeconds(10);
                        // change default of 0.5 seconds
                        intentRecognizer.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(5);
                        SpeechRecognitionResult result = await intentRecognizer.RecognizeAsync();
                        if (result.Status == SpeechRecognitionResultStatus.Success)
                        {
                            spokenWord = result.Text;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(spokenWord))
                {
                    if (!string.IsNullOrEmpty(spokenWord))
                    {
                        var result = await client.GetStringAsync(baseURL + "&sessionId=" + Guid.NewGuid().ToString() + 
                                "&query=" + Uri.EscapeUriString(spokenWord));
                        var results = JObject.Parse(result);
                        var output = (string)results["result"]["fulfillment"]["speech"];

                        await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>   {
                            PlayResponse(output); 
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                //log
            }
            finally
            {
                //result the main recognition session to listen for trigger word
                await recognizer.ContinuousRecognitionSession.StartAsync();
            }
        }

        // Initialize Speech Recognizer and start async recognition
        private async Task InitializeSpeechRecognizer()
        {
            try
            {
                // Initialize recognizer
                recognizer = new SpeechRecognizer();

                recognizer.ContinuousRecognitionSession.ResultGenerated += RecognizerResultGenerated;
                recognizer.Constraints.Add(new SpeechRecognitionListConstraint(new string[] { INVOCATION_NAME }));
                var compilationResult = await recognizer.CompileConstraintsAsync();
                if (compilationResult.Status == SpeechRecognitionResultStatus.Success){
                    if (recognizer.State == SpeechRecognizerState.Idle)
                    {
                        await recognizer.ContinuousRecognitionSession.StartAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                //log
            }
        }

        private async Task ProcessContinousVoiceResult(string target)
        {
            try
            {
                if (target.ToLower() == INVOCATION_NAME)
                {
                    await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        PlayResponse(INVOCATION_PROMPT);
                    });
                    await recognizer.ContinuousRecognitionSession.StopAsync();
                    await InitializeIntentRecognizer();

                }
            }
            catch (Exception ex)
            {
                //log
            }
        }

        // Recognizer generated results
        async private void RecognizerResultGenerated(SpeechContinuousRecognitionSession session, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            try
            {
                if (!string.IsNullOrEmpty(args.Result.Text))
                {
                    await ProcessContinousVoiceResult(args.Result.Text);
                }
            }
            catch (Exception ex)
            {
                //log
            }
        }

        private async Task PlayResponse(string text)
        {
            try
            {
                MediaElement media = new MediaElement();
                SpeechSynthesisStream stream = null;

                string Ssml =
                    @"<speak version='1.0' " +
                    "xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" +
                    text +
                    "</speak>";
  
                var voices = SpeechSynthesizer.AllVoices;
                using (var speech = new SpeechSynthesizer())
                {
                    speech.Voice = voices.First(gender => gender.Gender == VoiceGender.Male && gender.Description.Contains("David"));
                    stream = await speech.SynthesizeSsmlToStreamAsync(Ssml);
                }

                media.SetSource(stream, stream.ContentType);
                media.Play();

                media.Stop();
            }
            catch (Exception ex)
            {
                //log
            }
        }
    }
}
