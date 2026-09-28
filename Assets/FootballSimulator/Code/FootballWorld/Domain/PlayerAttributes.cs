namespace FStudio.FootballWorld.Domain
{
    public sealed class PlayerAttributes
    {
        public int Strength { get; }
        public int Acceleration { get; }
        public int TopSpeed { get; }
        public int DribbleSpeed { get; }
        public int Jump { get; }
        public int Tackling { get; }
        public int BallKeeping { get; }
        public int Passing { get; }
        public int LongBall { get; }
        public int Agility { get; }
        public int Shooting { get; }
        public int ShootPower { get; }
        public int Positioning { get; }
        public int Reaction { get; }
        public int BallControl { get; }

        public PlayerAttributes(
            int strength,
            int acceleration,
            int topSpeed,
            int dribbleSpeed,
            int jump,
            int tackling,
            int ballKeeping,
            int passing,
            int longBall,
            int agility,
            int shooting,
            int shootPower,
            int positioning,
            int reaction,
            int ballControl)
        {
            Strength = DomainValidation.InRange(strength, 0, 100, nameof(strength));
            Acceleration = DomainValidation.InRange(acceleration, 0, 100, nameof(acceleration));
            TopSpeed = DomainValidation.InRange(topSpeed, 0, 100, nameof(topSpeed));
            DribbleSpeed = DomainValidation.InRange(dribbleSpeed, 0, 100, nameof(dribbleSpeed));
            Jump = DomainValidation.InRange(jump, 0, 100, nameof(jump));
            Tackling = DomainValidation.InRange(tackling, 0, 100, nameof(tackling));
            BallKeeping = DomainValidation.InRange(ballKeeping, 0, 100, nameof(ballKeeping));
            Passing = DomainValidation.InRange(passing, 0, 100, nameof(passing));
            LongBall = DomainValidation.InRange(longBall, 0, 100, nameof(longBall));
            Agility = DomainValidation.InRange(agility, 0, 100, nameof(agility));
            Shooting = DomainValidation.InRange(shooting, 0, 100, nameof(shooting));
            ShootPower = DomainValidation.InRange(shootPower, 0, 100, nameof(shootPower));
            Positioning = DomainValidation.InRange(positioning, 0, 100, nameof(positioning));
            Reaction = DomainValidation.InRange(reaction, 0, 100, nameof(reaction));
            BallControl = DomainValidation.InRange(ballControl, 0, 100, nameof(ballControl));
        }
    }
}
