using System.Configuration;
using System;
using System.Globalization;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Intent;
using Microsoft.CognitiveServices.Speech.Translation;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Media;
using System.Xml.Linq;
using System.Net.Http;
using System.Text;

namespace SpeechService
{
    class Program
    {
        public static async Task RecognizeOnceSpeechAsync(SpeechTranslationConfig config)
        {
            
            var allCultures = CultureInfo.GetCultures(CultureTypes.AllCultures);

            // Creates a speech recognizer.
            using (var recognizer = new IntentRecognizer(config))
            {
                Console.WriteLine("Say something...");

                var model = LanguageUnderstandingModel.FromAppId(ConfigurationManager.AppSettings.Get("LUISId"));
                recognizer.AddAllIntents(model);

                var result = await recognizer.RecognizeOnceAsync();

                // Checks result.
                if (result.Reason == ResultReason.RecognizedIntent)
                {
                    Console.WriteLine($"RECOGNIZED: Text={result.Text}");
                    Console.WriteLine($"    Intent Id: {result.IntentId}.");
                    Console.WriteLine($"    Language Understanding JSON: {result.Properties.GetProperty(PropertyId.LanguageUnderstandingServiceResponse_JsonResult)}.");
                    if (result.IntentId == "Translate")
                    {
                        var luisJson = JObject.Parse(result.Properties.GetProperty(PropertyId.LanguageUnderstandingServiceResponse_JsonResult));
                        string targetLng = luisJson["entities"].First(x => x["type"].ToString() == "TargetLanguage")["entity"].ToString();
                        string text = luisJson["entities"].First(x => x["type"].ToString() == "Text")["entity"].ToString();
                        
                        var lng = allCultures.FirstOrDefault(c => c.DisplayName.ToLower() == targetLng.ToLower()) ??
                                  allCultures.FirstOrDefault(c => c.DisplayName.ToLower() == "english");
                        var translated = Translate.TranslateText("de-DE", text);

                        Console.WriteLine("Translation: " + translated);

                        var synth = new System.Speech.Synthesis.SpeechSynthesizer();

                        // Configure the audio output.   
                        synth.SetOutputToDefaultAudioDevice();

                        // Speak a string.  
                        synth.SelectVoice(synth.GetInstalledVoices().First(x => x.VoiceInfo.Culture.TwoLetterISOLanguageName == lng.TwoLetterISOLanguageName).VoiceInfo.Name);
                        synth.Speak(translated);
                    }
                }
                else if (result.Reason == ResultReason.RecognizedSpeech)
                {
                    Console.WriteLine($"RECOGNIZED: Text={result.Text}");
                    Console.WriteLine($"    Intent not recognized.");
                }
                else if (result.Reason == ResultReason.NoMatch)
                {
                    Console.WriteLine($"NOMATCH: Speech could not be recognized.");
                }
                else if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = CancellationDetails.FromResult(result);
                    Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        Console.WriteLine($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
                        Console.WriteLine($"CANCELED: Did you update the subscription info?");
                    }
                }
            }
        }

        public static async Task TTS(SpeechTranslationConfig config)
        {
            Console.Write("What would you like to convert to speech? ");
            string text = Console.ReadLine();

            string accessToken;
            Console.WriteLine("Attempting token exchange. Please wait...\n");

            Authentication auth = new Authentication(ConfigurationManager.AppSettings.Get("SpeechEndpoint"), config.SubscriptionKey);

            try
            {
                accessToken = await auth.FetchTokenAsync().ConfigureAwait(false);
                Console.WriteLine("Successfully obtained an access token. \n");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to obtain an access token.");
                Console.WriteLine(ex.ToString());
                Console.WriteLine(ex.Message);
                return;
            }

            string host = "https://eastus.tts.speech.microsoft.com/cognitiveservices/v1";

            const string GermanVoice = "de-DE, KatjaNeural";
            // const string EnglishNeural = "en-US, JessaNeural";
            config.VoiceName = GermanVoice;

            XDocument body = new XDocument(
                    new XElement("speak",
                        new XAttribute("version", "1.0"),
                        new XAttribute(XNamespace.Xml + "lang", "de-DE"),
                        new XElement("voice",
                            new XAttribute(XNamespace.Xml + "lang", "de-DE"),
                            new XAttribute("name", "Microsoft Server Speech Text to Speech Voice (" + GermanVoice +")"),
                            text)));

            using (HttpClient client = new HttpClient())
            {
                using (HttpRequestMessage request = new HttpRequestMessage())
                {
                    request.Method = HttpMethod.Post;
                    request.RequestUri = new Uri(host);
                    // Set the content type header
                    request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/ssml+xml");
                    // Set additional header, such as Authorization and User-Agent
                    request.Headers.Add("Authorization", "Bearer " + accessToken);
                    request.Headers.Add("Connection", "Keep-Alive");
                    request.Headers.Add("User-Agent", "TTSClient");
                    // Audio output format. See API reference for full list.
                    request.Headers.Add("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");
                    Console.WriteLine("Calling the TTS service. Please wait... \n");
                    using (HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        using (Stream dataStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        {
                            SoundPlayer snd = new SoundPlayer(dataStream);
                            snd.Play();
                        }
                    }
                }
            }
        }

        static void Main()
        {
            var config = SpeechTranslationConfig.FromSubscription(ConfigurationManager.AppSettings.Get("TTSKey"), ConfigurationManager.AppSettings.Get("Region"));
            //RecognizeOnceSpeechAsync(config).Wait();
            //Translate.RecognizeLng().Wait();
            Translate.TranslationContinuousRecognitionAsync(config).Wait();
            //TTS(config).Wait();
            Console.WriteLine("Please press a key to continue.");
            Console.ReadLine();
        }
    }
}