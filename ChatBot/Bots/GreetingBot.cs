using ChatBot.Models;
using ChatBot.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ChatBot.Bots
{
    public class GreetingBot : ActivityHandler
    {
        #region Variables
        private readonly StateService stateService;
        #endregion  

        public GreetingBot(StateService stateService)
        {
            stateService = stateService ?? throw new System.ArgumentNullException(nameof(stateService));
        }

        private async Task GetName(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            UserProfile userProfile = await stateService.UserProfileAccessor.GetAsync(turnContext, () => new UserProfile());
            ConversationData conversationData = await stateService.ConversationDataAccessor.GetAsync(turnContext, () => new ConversationData());
            if (!string.IsNullOrEmpty(userProfile.Name))
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(String.Format("Hi, {0}. My name is Stacy, a cleaning assistant bot. Would you like to set up a cleaning appointment?", userProfile.Name)), cancellationToken);
            }
            else
            {
                if (conversationData.PromptedUserForName)
                {
                    // Set the name to what the user provided
                    userProfile.Name = turnContext.Activity.Text?.Trim();

                    // Acknowledge that we got their name.
                    await turnContext.SendActivityAsync(MessageFactory.Text(String.Format("Thanks, {0}. My name is Margaret, a cleaning assistant bot. Would you like to set up a cleaning appointment?", userProfile.Name)), cancellationToken);

                    // Reset the flag to allow the bot to go though the cycle again.
                    conversationData.PromptedUserForName = false;
                }
                else
                {
                    // Prompt the user for their name.
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Hello, my name is Harriette, a cleaning assistant bot. Would you like to set up a cleaning appointment?"), cancellationToken);

                    // Set the flag to true, so we don't prompt in the next turn.
                    conversationData.PromptedUserForName = true;
                }

                // Save any state changes that might have occured during the turn.
                await stateService.UserProfileAccessor.SetAsync(turnContext, userProfile);
                await stateService.ConversationDataAccessor.SetAsync(turnContext, conversationData);

                await stateService.UserState.SaveChangesAsync(turnContext);
                await stateService.ConversationState.SaveChangesAsync(turnContext);
            }
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            await GetName(turnContext, cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await GetName(turnContext, cancellationToken);
                }
            }
        }

    }
}
