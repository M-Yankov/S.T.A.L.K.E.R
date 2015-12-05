namespace Santase.AI.StalkerPlayer.Contracts
{
    using System.Collections.Generic;
    using Common;
    using Logic.Cards;
    using Logic.Players;

    public interface IStalkerHelper
    {
        int GetCardPriority(Card card);

        bool CanCloseTheGame(PlayerTurnContext context, ICollection<Card> playerCards);

        Card GetCardWithSuitThatEnemyHasNot(bool enemyHasATrumpCard, CardSuit trumpSuit, ICollection<Card> playerCards);

        Card CheckForAnounce(CardSuit trumpSuit, int cardsLeftInDeck, string state, ICollection<Card> playerCards);

        bool ContainsGreaterCardThan(Card card, CardStatus status);

        bool ContainsLowerCardThan(Card card, CardStatus status);
    }
}
