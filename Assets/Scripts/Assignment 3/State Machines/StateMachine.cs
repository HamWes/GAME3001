using System;
using System.Collections.Generic;

// Small finite state machine used by the NPC controller.
public class StateMachine
{
    // The only NPC states required by the assignment.
    public enum NpcState
    {
        Idle,
        Patrol,
        MoveTowardsPlayer
    }

    public State InitialState { get; }
    public State CurrentState { get; private set; }
    public event Action<State, State, Transition> OnStateChanged;

    public StateMachine(State initialState)
    {
        InitialState = initialState;
        CurrentState = initialState;
    }

    public void Reset()
    {
        // Returns the machine to its starting state and runs that state's entry logic.
        CurrentState = InitialState;
        CurrentState?.OnEnter?.Invoke();
    }

    public void Update()
    {
        if (!TryTransition())
        {
            CurrentState?.OnUpdate?.Invoke();
        }
    }

    public bool TryTransition()
    {
        // Checks transitions in order and stops on the first one that succeeds.
        if (CurrentState == null)
        {
            return false;
        }

        foreach (Transition transition in CurrentState.Transitions)
        {
            if (!transition.IsTriggered())
            {
                continue;
            }

            State previousState = CurrentState;
            // Use the usual FSM order: exit, then enter the new state.
            previousState.OnExit?.Invoke();

            CurrentState = transition.TargetState;
            CurrentState?.OnEnter?.Invoke();

            OnStateChanged?.Invoke(previousState, CurrentState, transition);
            return true;
        }

        return false;
    }
}

// Represents one behavior mode for the NPC.
public class State
{
    private readonly List<Transition> transitions = new List<Transition>();

    public StateMachine.NpcState StateType { get; }
    public Action OnEnter { get; set; }
    public Action OnExit { get; set; }
    public Action OnUpdate { get; set; }
    public IReadOnlyList<Transition> Transitions => transitions;

    public State(StateMachine.NpcState stateType)
    {
        StateType = stateType;
    }

    public void AddTransition(Transition transition)
    {
        transitions.Add(transition);
    }
}

// Represents one possible move from the current state to another state.
public class Transition
{
    private readonly Func<bool> condition;

    public State TargetState { get; }

    public Transition(Func<bool> condition, State targetState)
    {
        this.condition = condition;
        TargetState = targetState;
    }

    public bool IsTriggered()
    {
        // Transition conditions are just lightweight bool callbacks.
        return condition != null && condition();
    }
}
