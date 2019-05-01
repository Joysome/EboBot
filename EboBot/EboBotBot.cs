// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace EboBot
{
    /// <summary>
    /// Represents a bot that processes incoming activities.
    /// For each user interaction, an instance of this class is created and the OnTurnAsync method is called.
    /// This is a Transient lifetime service.  Transient lifetime services are created
    /// each time they're requested. For each Activity received, a new instance of this
    /// class is created. Objects that are expensive to construct, or have a lifetime
    /// beyond the single turn, should be carefully managed.
    /// For example, the <see cref="MemoryStorage"/> object and associated
    /// <see cref="IStatePropertyAccessor{T}"/> object are created with a singleton lifetime.
    /// </summary>
    /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1"/>
    public class EboBotBot : IBot
    {
        private readonly EboBotAccessors _accessors;
        private readonly WelcomeUserStateAccessors _welcomeUserStateAccessors;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="accessors">A class containing <see cref="IStatePropertyAccessor{T}"/> used to manage state.</param>
        /// <param name="loggerFactory">A <see cref="ILoggerFactory"/> that is hooked to the Azure App Service provider.</param>
        /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1#windows-eventlog-provider"/>
        public EboBotBot(EboBotAccessors accessors, WelcomeUserStateAccessors welcomeUserStateAccessors, ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<EboBotBot>();
            _logger.LogTrace("Turn start.");
            _accessors = accessors ?? throw new System.ArgumentNullException(nameof(accessors));
            _welcomeUserStateAccessors = welcomeUserStateAccessors ?? throw new System.ArgumentNullException(nameof(welcomeUserStateAccessors));
        }

        /// <summary>
        /// Every conversation turn for our Echo Bot will call this method.
        /// There are no dialogs used, since it's "single turn" processing, meaning a single
        /// request and response.
        /// </summary>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn. </param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        /// <seealso cref="BotStateSet"/>
        /// <seealso cref="ConversationState"/>
        /// <seealso cref="IMiddleware"/>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            // use state accessor to extract the didBotWelcomeUser flag
            var didBotWelcomeUser = await _welcomeUserStateAccessors.WelcomeUserState.GetAsync(turnContext, () => new WelcomeUserState());

            // Handle Message activity type, which is the main activity type for shown within a conversational interface
            // Message activities may contain text, speech, interactive cards, and binary or unknown attachments.
            // see https://aka.ms/about-bot-activity-message to learn more about the message and other activity types
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                // Your bot should proactively send a welcome message to a personal chat the first time
                // (and only the first time) a user initiates a personal chat with your bot.
                if (didBotWelcomeUser.DidBotWelcomeUser == false)
                {
                    didBotWelcomeUser.DidBotWelcomeUser = true;
                    // Update user state flag to reflect bot handled first user interaction.
                    await _welcomeUserStateAccessors.WelcomeUserState.SetAsync(turnContext, didBotWelcomeUser);
                    await _welcomeUserStateAccessors.UserState.SaveChangesAsync(turnContext);

                    // the channel should sends the user name in the 'From' object
                    var userName = turnContext.Activity.From.Name;

                    await turnContext.SendActivityAsync($"You are seeing this message because this was your first message ever to this bot.", cancellationToken: cancellationToken);
                    await turnContext.SendActivityAsync($"It is a good practice to welcome the user and provide personal greeting. For example, welcome {userName}.", cancellationToken: cancellationToken);
                }
                else
                {
                    // This example hardcodes specific utterances. You should use LUIS or QnA for more advance language understanding.
                    var text = turnContext.Activity.Text.ToLowerInvariant();
                    switch (text)
                    {
                        case "hello":
                        case "hi":
                            await turnContext.SendActivityAsync($"You said {text}.", cancellationToken: cancellationToken);
                            break;
                        case "intro":
                        //case "help":
                        //    await SendIntroCardAsync(turnContext, cancellationToken);
                        //    break;
                        default:
                            //await turnContext.SendActivityAsync(WelcomeMessage, cancellationToken: cancellationToken);
                            break;
                    }
                }

                // Handle Message activity type, which is the main activity type for shown within a conversational interface
                // Message activities may contain text, speech, interactive cards, and binary or unknown attachments.
                // see https://aka.ms/about-bot-activity-message to learn more about the message and other activity types
                if (turnContext.Activity.Type == ActivityTypes.Message)
                {

                    if (turnContext.Activity.Text == "image")
                    {
                        await SendImageTest(turnContext);
                        return;
                    }

                    if (turnContext.Activity.Text == "card")
                    {
                        await SendCardTest(turnContext);
                        return;
                    }

                    await IncreaseMessageCounterTest(turnContext, _accessors);
                    await SendSimpleEchoTest(turnContext, _accessors);
                }
            }
            // Greet when users are added to the conversation.
            // Note that all channels do not send the conversation update activity.
            // If you find that this bot works in the emulator, but does not in
            // another channel the reason is most likely that the channel does not
            // send this activity.
            else if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                if (turnContext.Activity.MembersAdded != null)
                {
                    // Iterate over all new members added to the conversation
                    foreach (var member in turnContext.Activity.MembersAdded)
                    {
                        // Greet anyone that was not the target (recipient) of this message
                        // the 'bot' is the recipient for events from the channel,
                        // turnContext.Activity.MembersAdded == turnContext.Activity.Recipient.Id indicates the
                        // bot was added to the conversation.
                        if (member.Id != turnContext.Activity.Recipient.Id)
                        {
                            await turnContext.SendActivityAsync($"Hi there - {member.Name}. This message shows that you've just joined the channel with this bot.", cancellationToken: cancellationToken);
                        }
                    }
                }
            }
            else
            {
                // Default behavior for all other type of activities.
                await turnContext.SendActivityAsync($"{turnContext.Activity.Type} activity detected");
            }
        }

        private async Task SendSimpleEchoTest(ITurnContext turnContext, EboBotAccessors accessors)
        {
            // Get the conversation state from the turn context.
            var state = await accessors.CounterState.GetAsync(turnContext, () => new CounterState());

            // Echo back to the user whatever they typed.
            var responseMessage = $"Turn {state.TurnCount}: You sent '{turnContext.Activity.Text}'\n";
            await turnContext.SendActivityAsync(responseMessage);
        }

        private async Task IncreaseMessageCounterTest(ITurnContext turnContext, EboBotAccessors accessors)
        {
            // Get the conversation state from the turn context.
            var state = await accessors.CounterState.GetAsync(turnContext, () => new CounterState());

            // Bump the turn count for this conversation.
            state.TurnCount++;

            // Set the property using the accessor.
            await _accessors.CounterState.SetAsync(turnContext, state);

            // Save the new turn count into the conversation state.
            await _accessors.ConversationState.SaveChangesAsync(turnContext);
        }

        private async Task SendImageTest(ITurnContext turnContext)
        {

            var reply = turnContext.Activity.CreateReply();
            // Create an attachment.
            var attachment = new Attachment
            {
                ContentUrl = "https://docs.microsoft.com/en-us/dotnet/standard/microservices-architecture/media/cover-small.png",
                ContentType = "image/png",
                Name = "imageName",
            };
            // Add the attachment to our reply.
            reply.Attachments = new List<Attachment>() { attachment };

            // Send the activity to the user.
            await turnContext.SendActivityAsync(reply);
        }

        private async Task SendCardTest(ITurnContext turnContext)
        {
            var reply = turnContext.Activity.CreateReply("What is your favorite color?");

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                        {
                            new CardAction() { Title = "Red", Type = ActionTypes.ImBack, Value = "Red" },
                            new CardAction() { Title = "Yellow", Type = ActionTypes.ImBack, Value = "Yellow" },
                            new CardAction() { Title = "Blue", Type = ActionTypes.ImBack, Value = "Blue" },
                        },

            };
            await turnContext.SendActivityAsync(reply);
        }

        private async Task SendWelcomeMessageAsync(ITurnContext turnContext)
        {
            // Check to see if any new members were added to the conversation.
            if (turnContext.Activity.MembersAdded.Count > 0)
            {
                // Iterate over all new members added to the conversation.
                foreach (var member in turnContext.Activity.MembersAdded)
                {
                    // Greet anyone that was not the target (recipient) of this message
                    // the 'bot' is the recipient for events from the channel,
                    // turnContext.activity.membersAdded == turnContext.activity.recipient.Id indicates the
                    // bot was added to the conversation.
                    if (member.Id != turnContext.Activity.Recipient.Id)
                    {
                        await turnContext.SendActivityAsync("Hello, this is a welcome message.");
                        await turnContext.SendActivityAsync("Please, stand by...");
                        //var activity = turnContext.Activity.CreateReply();
                        //activity.Attachments = new List<Attachment> { Helpers.CreateAdaptiveCardAttachment(new[] { ".", "Dialogs", "Welcome", "Resources", "welcomeCard.json" }), };

                        // Send welcome card.
                        //await turnContext.SendActivityAsync(activity);
                    }
                }
            }
        }
    }
}
