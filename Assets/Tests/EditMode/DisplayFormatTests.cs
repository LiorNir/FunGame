using FunGame.Battle;
using NUnit.Framework;

namespace FunGame.Board.Tests
{
    public class DisplayFormatTests
    {
        [Test]
        public void Values_AreCutToTwoDecimals_NotRounded()
        {
            Assert.AreEqual("10.57", DisplayFormat.Value(10.5783f));
            Assert.AreEqual("0.66", DisplayFormat.Value(2f / 3f));
            Assert.AreEqual("0.33", DisplayFormat.Value(1f / 3f));
        }

        [Test]
        public void TrailingZeros_AreDropped()
        {
            Assert.AreEqual("20", DisplayFormat.Value(20f));
            Assert.AreEqual("1.5", DisplayFormat.Value(1.5f));
            Assert.AreEqual("0", DisplayFormat.Value(0f));
        }

        [Test]
        public void AValueAHairUnderAWholeNumber_ShowsAsTheWholeNumber()
        {
            Assert.AreEqual("1", DisplayFormat.Value(0.99999994f));
            Assert.AreEqual("3", DisplayFormat.Value(2.9999998f));
        }

        [Test]
        public void AValueAboveZero_NeverShowsAsZero()
        {
            Assert.AreEqual("0.01", DisplayFormat.Value(0.004f));
            Assert.AreEqual("0", DisplayFormat.Value(0.00001f));
        }
    }
}
