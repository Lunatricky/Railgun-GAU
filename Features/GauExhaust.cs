namespace IngameScript.Domain
{
    partial class GauGeo
    {

            public void TriggerExhaustEffect()
        {
            // === TURNING ON ===
            if (_state < exhaustLists.Count)
            {
                if (_tickCounter >= _stepDelayTicks)
                {
                    exhaustLists[_state].ForEach(exhaust => exhaust.Enabled = true);
                    _state++;
                    _tickCounter = 0;
                }
                else
                {
                    _tickCounter++;
                }
            }
        }

        public void ExhaustOff()
        {
            // === TURNING OFF ===
            exhaustLists.ForEach(exhaustList => exhaustList.ForEach(exhaust => exhaust.Enabled = false));
        }

        public void ExhaustReset()
        {
            _state = 0;
            _tickCounter = 0;
            _exhaustEffectDelay = (_stepDelayTicks + 1) * exhaustLists.Count;
        }
    }
}
