namespace Santase.AI.StalkerPlayer.Contracts
{
    using System.Collections.Generic;

    using Santase.Logic.Cards;
    using Santase.Logic.Players;

    public interface ICardChooser
    {
        Card ChooseCardToPlay(PlayerTurnContext context, ICollection<Card> stalkerCards);
    }
}