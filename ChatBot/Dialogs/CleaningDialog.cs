using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using ChatBot.Models;
using ChatBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ChatBot.Dialogs
{
    public class CleaningDialog : ComponentDialog

    {
        #region Variables
        private readonly StateService _stateService;
        #endregion  


        public CleaningDialog(string dialogId, StateService stateService) : base(dialogId)
        {
            _stateService = stateService ?? throw new System.ArgumentNullException(nameof(stateService));

            InitializeWaterfallDialog();
        }

        private void InitializeWaterfallDialog()
        {
            // Create Waterfall Steps
            var waterfallSteps = new WaterfallStep[]
            {
                DescriptionStepAsync,
                CleaningTimeStepAsync,
                PhoneNumberStepAsync,
                CleanStepAsync,
                SummaryStepAsync,
                //CorrectStepAsync,
                ContactStepAsync
            };

            // Add Named Dialogs
            AddDialog(new WaterfallDialog($"{nameof(CleaningDialog)}.mainFlow", waterfallSteps));
            AddDialog(new TextPrompt($"{nameof(CleaningDialog)}.description"));
            AddDialog(new DateTimePrompt($"{nameof(CleaningDialog)}.cleaningTime", CallbackTimeValidatorAsync));
            AddDialog(new TextPrompt($"{nameof(CleaningDialog)}.phoneNumber", PhoneNumberValidatorAsync));
            AddDialog(new ChoicePrompt($"{nameof(CleaningDialog)}.clean"));
            AddDialog(new TextPrompt($"{nameof(CleaningDialog)}.summary"));
            AddDialog(new TextPrompt($"{nameof(CleaningDialog)}.contact"));

            // Set the starting Dialog
            InitialDialogId = $"{nameof(CleaningDialog)}.mainFlow";
        }

        #region Waterfall Steps
        private async Task<DialogTurnResult> DescriptionStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync($"{nameof(CleaningDialog)}.description",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Please enter a description of anything specifically that needs cleaning.")
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> CleaningTimeStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["description"] = (string)stepContext.Result;

            return await stepContext.PromptAsync($"{nameof(CleaningDialog)}.cleaningTime",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Please enter a date (MM/DD/YYY) and time that you would like a cleaning to be scheduled."),
                    RetryPrompt = MessageFactory.Text("The value entered must be between the hours of 9 am and 5 pm."),
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> PhoneNumberStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["cleaningTime"] = Convert.ToDateTime(((List<DateTimeResolution>)stepContext.Result).FirstOrDefault().Value);

            return await stepContext.PromptAsync($"{nameof(CleaningDialog)}.phoneNumber",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Please enter in a phone number that we can call you at"),
                    RetryPrompt = MessageFactory.Text("Please enter a valid phone number"),
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> CleanStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["phoneNumber"] = (string)stepContext.Result;

            return await stepContext.PromptAsync($"{nameof(CleaningDialog)}.clean",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Please choose the type of clean you want."),
                    Choices = ChoiceFactory.ToChoices(Common.CleanTypes),
                    Style = ListStyle.HeroCard,
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> SummaryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["clean"] = ((FoundChoice)stepContext.Result).Value;

            // Get the current profile object from user state.
            var userProfile = await _stateService.UserProfileAccessor.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);

            // Save all of the data inside the user profile
            userProfile.Description = (string)stepContext.Values["description"];
            userProfile.CleaningTime = (DateTime)stepContext.Values["cleaningTime"];
            userProfile.PhoneNumber = (string)stepContext.Values["phoneNumber"];
            userProfile.Clean = (string)stepContext.Values["clean"];

            // Show the summary to the user
            await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Here is a summary of your preferrred appointment:"), cancellationToken);
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(String.Format("Description: {0}", userProfile.Description)), cancellationToken);
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(String.Format("Cleaning Time: {0}", userProfile.CleaningTime.ToString())), cancellationToken);
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(String.Format("Phone Number: {0}", userProfile.PhoneNumber)), cancellationToken);
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(String.Format("Type of clean: {0}", userProfile.Clean)), cancellationToken);

            // Save data in userstate
            await _stateService.UserProfileAccessor.SetAsync(stepContext.Context, userProfile);

            return await stepContext.PromptAsync($"{nameof(CleaningDialog)}.summary",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Does the information look correct?")
                }, cancellationToken);

            
        }

        private async Task<DialogTurnResult> ContactStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
             await stepContext.PromptAsync($"{nameof(CleaningDialog)}.contact",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Great, we will contact you within two business days to confirm appointment. If you need to call, you can reach us at, 406-333-2525. Thank you and have a wonderful day.")
                }, cancellationToken);

            // WaterfallStep always finishes with the end of the Waterfall or with another dialog, here it is the end.
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        #endregion

        #region Validators
        private Task<bool> CallbackTimeValidatorAsync(PromptValidatorContext<IList<DateTimeResolution>> promptContext, CancellationToken cancellationToken)
        {
            var valid = false;

            if (promptContext.Recognized.Succeeded)
            {
                var resolution = promptContext.Recognized.Value.First();
                DateTime selectedDate = Convert.ToDateTime(resolution.Value);
                TimeSpan start = new TimeSpan(9, 0, 0); //9 o'clock
                TimeSpan end = new TimeSpan(17, 0, 0); //5 o'clock
                if ((selectedDate.TimeOfDay >= start) && (selectedDate.TimeOfDay <= end))
                {
                    valid = true;
                }
            }
            return Task.FromResult(valid);
        }

        private Task<bool> PhoneNumberValidatorAsync(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var valid = false;

            if (promptContext.Recognized.Succeeded)
            {
                valid = Regex.Match(promptContext.Recognized.Value, @"^(\+\d{1,2}\s)?\(?\d{3}\)?[\s.-]?\d{3}[\s.-]?\d{4}$").Success;
            }
            return Task.FromResult(valid);
        }
        #endregion
    } 
}

