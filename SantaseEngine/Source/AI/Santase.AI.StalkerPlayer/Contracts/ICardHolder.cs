namespace Santase.AI.StalkerPlayer.Contracts
{
    using System.Collections.Generic;

    using Santase.AI.StalkerPlayer.Common;
    using Santase.Logic.Cards;

    public interface ICardHolder
    {
        IDictionary<CardSuit, Dictionary<CardType, CardStatus>> AllCards { get; set; }

        ISet<Card> EnemyCards { get; set; }

        void Initialize(ICollection<Card> cards);

        void RefreshEnemyCards();
    }
}