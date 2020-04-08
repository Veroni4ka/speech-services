using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SpeechService
{
    class Translate
    {
        public static string TranslateText(string lng, string text)
        {
            string host = "https://api.cognitive.microsofttranslator.com";
            string route = "/translate?api-version=3.0&to=" + lng;
            string subscriptionKey = ConfigurationManager.AppSettings.Get("TextTranslate");

            System.Object[] body = new System.Object[] { new { Text = text } };
            var requestBody = JsonConvert.SerializeObject(body);

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                // Set the method to POST
                request.Method = HttpMethod.Post;
                // Construct the full URI
                request.RequestUri = new Uri(host + route);
                // Add the serialized JSON object to your request
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                // Add the authorization header
                request.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
                // Send request, get response
                var response = client.SendAsync(request).Result;
                var jsonResponse = JArray.Parse(response.Content.ReadAsStringAsync().Result);
                var result = JObject.Parse(jsonResponse[0].ToString())["translations"].First()["text"].ToString();

                return result;
            }
        }

        public static async Task RecognizeLng()
        {
            SpeechConfig speechConfig = SpeechConfig.FromEndpoint(new System.Uri(ConfigurationManager.AppSettings.Get("SpeechEndpoint")), ConfigurationManager.AppSettings.Get("TTSKey"));
            AudioConfig audioConfig = AudioConfig.FromDefaultSpeakerOutput();
            AutoDetectSourceLanguageConfig autoDetectSourceLanguageConfig = AutoDetectSourceLanguageConfig
                                                        .FromLanguages(new string[] { "en-US", "ru-RU" });
            
            using (var recognizer = new SpeechRecognizer(
                speechConfig,
                autoDetectSourceLanguageConfig,
                audioConfig))
            {
                Console.WriteLine("Say something...");
                var speechRecognitionResult = await recognizer.RecognizeOnceAsync();
                var autoDetectSourceLanguageResult =
                    AutoDetectSourceLanguageResult.FromResult(speechRecognitionResult);
                var detectedLng = autoDetectSourceLanguageResult.Language;
                Console.WriteLine("I recognized " + speechRecognitionResult.Text + " in " + detectedLng);
            }
        }

        public static async Task TranslationContinuousRecognitionAsync(SpeechTranslationConfig config)
        {
            byte[] audio = null;
            string fromLanguage = "en-US";
            #region LanguageDetection
            /*SpeechConfig speechConfig = SpeechConfig.FromEndpoint(new System.Uri(ConfigurationManager.AppSettings.Get("SpeechEndpoint")), ConfigurationManager.AppSettings.Get("TTSKey"));
            AudioConfig audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            string fromLanguage = string.Empty;
            AutoDetectSourceLanguageConfig autoDetectSourceLanguageConfig = AutoDetectSourceLanguageConfig
                                                        .FromLanguages(new string[] { "en-US", "ru-RU" });
            using (var recognizer = new SpeechRecognizer(
                speechConfig, 
                autoDetectSourceLanguageConfig,
                audioConfig))
            {
                Console.WriteLine("Say something...");
                var speechRecognitionResult = await recognizer.RecognizeOnceAsync();
                var autoDetectSourceLanguageResult =
                    AutoDetectSourceLanguageResult.FromResult(speechRecognitionResult);
                fromLanguage = autoDetectSourceLanguageResult.Language;
                Console.WriteLine("I recognized " + speechRecognitionResult.Text + " in " + fromLanguage);
            }*/
            #endregion
            config.SpeechRecognitionLanguage = fromLanguage;
            config.AddTargetLanguage("de");

            const string GermanVoice = "de-DE-Hedda";
            config.VoiceName = GermanVoice;
            // Creates a translation recognizer using microphone as audio input.
            using (var recognizer = new TranslationRecognizer(config))
            {
                recognizer.Recognizing += (s, e) =>
                {
                    Console.WriteLine($"RECOGNIZING in '{fromLanguage}': Text={e.Result.Text}");
                    foreach (var element in e.Result.Translations)
                    {
                        Console.WriteLine($"    TRANSLATING into '{element.Key}': {element.Value}");
                    }
                };

                recognizer.Recognized += (s, e) =>
                {
                    if (e.Result.Reason == ResultReason.TranslatedSpeech)
                    {
                        Console.WriteLine($"\nFinal result: Reason: {e.Result.Reason.ToString()}, recognized text in {fromLanguage}: {e.Result.Text}.");
                        foreach (var element in e.Result.Translations)
                        {
                            Console.WriteLine($"    TRANSLATING into '{element.Key}': {element.Value}");
                        }
                    }
                };

                recognizer.Synthesizing += (s, e) =>
                {
                    audio = e.Result.GetAudio();
                    Console.WriteLine(audio.Length != 0
                        ? $"AudioSize: {audio.Length}"
                        : $"AudioSize: {audio.Length} (end of synthesis data)");
                    using (MemoryStream ms = new MemoryStream(audio))
                    {
                        SoundPlayer player = new SoundPlayer();
                        player.Stream = null;
                        player.Stream = ms;
                        player.Stream.Position = 0;
                        player.PlaySync();
                    }     
                };

                recognizer.Canceled += (s, e) =>
                {
                    Console.WriteLine($"\nRecognition canceled. Reason: {e.Reason}; ErrorDetails: {e.ErrorDetails}");
                };

                recognizer.SessionStarted += (s, e) =>
                {
                    Console.WriteLine("\nSession started event.");
                };

                recognizer.SessionStopped += (s, e) =>
                {
                    Console.WriteLine("\nSession stopped event.");
                };

                // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
                Console.WriteLine("Say something...");
                await recognizer.RecognizeOnceAsync();//.StartContinuousRecognitionAsync().ConfigureAwait(false);

                do
                {
                    Console.WriteLine("Press Enter to stop");
                } while (Console.ReadKey().Key != ConsoleKey.Enter);


                // Stops continuous recognition.
                await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            }
        }
    }
}
