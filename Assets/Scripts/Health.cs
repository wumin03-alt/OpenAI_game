using System;
using UnityEngine;

namespace AdaptiveBossPrototype
{
    public sealed class Health : MonoBehaviour
    {
        public Team Team { get; private set; }
        public float Current { get; private set; }
        public float Maximum { get; private set; }
        public float Normalized => Maximum <= 0f ? 0f : Current / Maximum;
        public bool IsDead { get; private set; }

        public event Action<Health, float> Damaged;
        public event Action<Health> Died;

        public void Configure(float maximum, Team team)
        {
            Maximum = maximum;
            Current = maximum;
            Team = team;
            IsDead = false;
        }

        public bool Damage(float amount)
        {
            if (IsDead || amount <= 0f) return false;
            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(this, amount);
            if (Current <= 0f)
            {
                IsDead = true;
                Died?.Invoke(this);
            }
            return true;
        }

        public void RestoreFull()
        {
            Current = Maximum;
            IsDead = false;
        }
    }
}
