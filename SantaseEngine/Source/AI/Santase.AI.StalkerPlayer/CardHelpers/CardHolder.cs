namespace Santase.AI.StalkerPlayer.CardHelpers
{
    using System.Collections.Generic;

    using Santase.AI.StalkerPlayer.Common;
    using Santase.AI.StalkerPlayer.Contracts;
    using Santase.Logic.Cards;

    public class CardHolder : ICardHolder
    {
        private readonly CardSuit[] cardSuits;
        private readonly CardType[] cardTypes;
        private IDictionary<CardSuit, Dictionary<CardType, CardStatus>> allCards;
        private ISet<Card> enemyCards;

        public CardHolder()
        {
            this.cardSuits = new[] { CardSuit.Club, CardSuit.Diamond, CardSuit.Heart, CardSuit.Spade };
            this.cardTypes = new[] { CardType.Ace, CardType.Ten, CardType.King, CardType.Queen, CardType.Jack, CardType.Nine };

            this.enemyCards = new HashSet<Card>();

            this.AllCards = new Dictionary<CardSuit, Dictionary<CardType, CardStatus>>();
            this.AllCards.Add(CardSuit.Club, new Dictionary<CardType, CardStatus>());
            this.AllCards.Add(CardSuit.Diamond, new Dictionary<CardType, CardStatus>());
            this.AllCards.Add(CardSuit.Heart, new Dictionary<CardType, CardStatus>());
            this.AllCards.Add(CardSuit.Spade, new Dictionary<CardType, CardStatus>());
        }

        public IDictionary<CardSuit, Dictionary<CardType, CardStatus>> AllCards
        {
            get { return this.allCards; }
            set { this.allCards = value; }
        }

        public ISet<Card> EnemyCards
        {
            get { return this.enemyCards; }
            set { this.enemyCards = value; }
        }

        public void Initialize(ICollection<Card> cards)
        {
            foreach (var cardSuit in this.cardSuits)
            {
                foreach (var cardType in this.cardTypes)
                {
                    if (!cards.Contains(new Card(cardSuit, cardType)))
                    {
                        this.AllCards[cardSuit][cardType] = CardStatus.InDeckOrEnemy;
                    }
                    else
                    {
                        this.AllCards[cardSuit][cardType] = CardStatus.InStalker;
                    }
                }
            }
        }

        public void RefreshEnemyCards()
        {
            var refreshedEnemyCards = new HashSet<Card>();

            foreach (CardSuit cardSuit in this.cardSuits)
            {
                foreach (CardType cardType in this.cardTypes)
                {
                    if (this.AllCards[cardSuit][cardType] == CardStatus.InDeckOrEnemy || this.AllCards[cardSuit][cardType] == CardStatus.InEnemy)
                    {
                        this.AllCards[cardSuit][cardType] = CardStatus.InEnemy;
                        refreshedEnemyCards.Add(new Card(cardSuit, cardType));
                    }
                }
            }

            this.enemyCards = refreshedEnemyCards;
        }
    }
}
