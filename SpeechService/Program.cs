using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Intent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SpeechService
{
    class Program
    {
        public static async Task RecognizeSpeechAsync()
        {
            var config = SpeechTranslationConfig.FromSubscription("421bec868d334e249897e9b2432af7b9", "eastus2");
            var allCultures = CultureInfo.GetCultures(CultureTypes.AllCultures);

            // Creates a speech recognizer.
            using (var recognizer = new IntentRecognizer(config))
            {
                Console.WriteLine("Say something...");

                var model = LanguageUnderstandingModel.FromAppId("65332712-8c79-41b5-86bb-841c663e210a");
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
                        string lng = luisJson["entities"].Where(x => x["type"].ToString() == "Translate.TargetLanguage").First()["entity"].ToString();
                        string text = luisJson["entities"].Where(x => x["type"].ToString() == "Translate.Text").First()["entity"].ToString();
                        
                        var lngName = allCultures.FirstOrDefault(c => c.DisplayName.ToLower() == lng.ToLower()).Name;

                        Console.WriteLine(Translate.TranslateText(lngName, text));
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

        static void Main()
        {
            RecognizeSpeechAsync().Wait();
            Console.WriteLine("Please press a key to continue.");
            Console.ReadLine();
        }
    }
}