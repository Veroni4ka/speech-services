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

                // Starts speech recognition, and returns after a single utterance is recognized. The end of a
                // single utterance is determined by listening for silence at the end or until a maximum of 15
                // seconds of audio is processed.  The task returns the recognition text as result. 
                // Note: Since RecognizeOnceAsync() returns only a single utterance, it is suitable only for single
                // shot recognition like command or query. 
                // For long-running multi-utterance recognition, use StartContinuousRecognitionAsync() instead.
                var result = await recognizer.RecognizeOnceAsync();

                // Checks result.
                if (result.Reason == ResultReason.RecognizedIntent)
                {
                    Console.WriteLine($"RECOGNIZED: Text={result.Text}");
                    Console.WriteLine($"    Intent Id: {result.IntentId}.");
                    Console.WriteLine($"    Language Understanding JSON: {result.Properties.GetProperty(PropertyId.LanguageUnderstandingServiceResponse_JsonResult)}.");
                    if (result.IntentId == "Translate.Translate")
                    {

                        var luisJson = JObject.Parse(result.Properties.GetProperty(PropertyId.LanguageUnderstandingServiceResponse_JsonResult));
                        string targetLng = luisJson["entities"].First(x => x["type"].ToString() == "Translate.TargetLanguage")["entity"].ToString();
                        string text = luisJson["entities"].First(x => x["type"].ToString() == "Translate.Text")["entity"].ToString();
                        
                        var lng = allCultures.FirstOrDefault(c => c.DisplayName.ToLower() == targetLng.ToLower()) ??
                                  allCultures.FirstOrDefault(c => c.DisplayName.ToLower() == "english");
                        var translated = Translate.TranslateText(lng?.Name, text);

                        Console.WriteLine(translated);

                        var synth = new SpeechSynthesizer();

                        // Configure the audio output.   
                        synth.SetOutputToDefaultAudioDevice();

                        // Speak a string.  
                        synth.SelectVoice(synth.GetInstalledVoices(lng).First().VoiceInfo.Name);
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

        public static async Task TranslationContinuousRecognitionAsync(SpeechTranslationConfig config)
        {
            // Sets source and target languages.
            string fromLanguage = "en-US";
            config.SpeechRecognitionLanguage = fromLanguage;
            config.AddTargetLanguage("de");

            // Sets voice name of synthesis output.
            const string GermanVoice = "de-DE-Hedda";
            // const string EnglishNeural = "en-US, JessaNeural";
            config.VoiceName = GermanVoice;
            // Creates a translation recognizer using microphone as audio input.
            using (var recognizer = new TranslationRecognizer(config))
            {

                // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
                Console.WriteLine("Say something...");
                await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

                do
                {
                    Console.WriteLine("Press Enter to stop");
                } while (Console.ReadKey().Key != ConsoleKey.Enter);

                // Stops continuous recognition.
                await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            }
        }

        static void Main()
        {
            var config = SpeechTranslationConfig.FromSubscription(ConfigurationManager.AppSettings.Get("SpeechKey"), ConfigurationManager.AppSettings.Get("Region"));
            RecognizeOnceSpeechAsync(config).Wait();
            //TranslationContinuousRecognitionAsync(config).Wait();
            Console.WriteLine("Please press a key to continue.");
            Console.ReadLine();
        }
    }
}