using System.Globalization;
using System.Numerics;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;

namespace Fitness
{
    /* This is now a horrible mix of functional and imperative code:
    - in principle Aggregate() would handle the state for us
    - but we've now constructed the whole mess in such a way that the generic part is taken care off by destructively modifying the
        PropertyCache with our elements derived from the structure of the FOp...
    - the elements in the cache will be derived from the hash code of the operation which should make them unique.
    - Since we're always reading from the input cache and writing to the output cache, you can safely call Eval() multiple times.
    - TODO: an if-then-else-like projection, comparisons...
    */
    internal class AccState
    {

        // We abuse the property-cache to keep state:
        public IDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>() { };

        internal void Set(Property prop, object value)
        {
            Properties[prop.Name] = value;
        }

        internal object Get(Property prop)
        {
            Properties.TryGetValue(prop.Name, out object outP);
            return outP;
        }

        public AccState(Fitness fitness)
        {
            foreach (var o in fitness.FOps)
            {
                // Could probably be nicer/Zip...
                var ivs = o.MkInitialValues(fitness.previous).GetEnumerator();
                foreach (var p in o.MkProps())
                {
                    ivs.MoveNext();
                    Set(p, ivs.Current);
                }
            }
        }
    }

    public class Fitness(Simulation simulation)
    {
        public Simulation previous = simulation;
        // We support multiple "root" expressions.
        required public IEnumerable<FOp> FOps { get; init; }

        internal AccState Process(AccState state, Simulation simulation)
        {
            // Update `state` in place:
            foreach (FOp fop in FOps)
            {
                fop.Eval(state, simulation, state);
                // XXX: review?
                simulation.PropertyCache.Properties[fop.Prop.Name] = new Property { Name = fop.Prop.Name, Value = state.Properties[fop.Prop.Name], OwlType = fop.Prop.OwlType };
            }
            previous = simulation;
            return state;
        }

        internal AccState MkState()
        {
            return new AccState(this);
        }
    }

    public abstract class FOp
    {
        internal abstract void Eval(AccState in_state, Simulation sim, AccState out_state);
        internal abstract IEnumerable<object> MkInitialValues(Simulation s);
        internal abstract IEnumerable<Property> MkProps();
        // If we need a value, that's where it is:
        public Property Prop { get; set; }
    }

    public class FProp : FOp
    {
        public FProp(string name, string type)
        { // TODO: Use Property directly?
            this.Prop = new Property() { OwlType = type, Name = name, Value = null };
        }

        internal override IEnumerable<object> MkInitialValues(Simulation s)
        {
            return new object[] { s.PropertyCache.Properties[Prop.Name].Value };
        }

        internal override IEnumerable<Property> MkProps()
        {
            return new[] { Prop };
        }

        internal override void Eval(AccState in_state, Simulation sim, AccState out_state)
        {
            // Copy current value into output:
            out_state.Set(Prop, sim.PropertyCache.Properties[Prop.Name].Value);
        }
    }
    public abstract class FBinOp<T> : FOp
    {
        public FBinOp(FOp left, FOp right, String? name = null)
        {
            this.L = left;
            this.R = right;
            // XXX Other datatypes...
            this.Prop = new Property() { OwlType = L.Prop.OwlType, Name = name ?? GetHashCode().ToString() + "_BinOp", Value = null };
        }

        internal override IEnumerable<object> MkInitialValues(Simulation s)
        {
            var l = L.MkInitialValues(s);
            var r = R.MkInitialValues(s);
            return new object[] { 0 }.Concat(l).Concat(r);
        }

        internal override IEnumerable<Property> MkProps()
        {
            return new[] { Prop }.Concat(L.MkProps()).Concat(R.MkProps());
        }

        internal override void Eval(AccState in_state, Simulation sim, AccState out_state)
        {
            // Evaluate both sides independently:
            L.Eval(in_state, sim, out_state);
            R.Eval(in_state, sim, out_state);
            out_state.Set(Prop, Operation(out_state.Get(L.Prop), out_state.Get(R.Prop)));
        }

        // TODO: would be nice to have a type parameter here tied to the OwlType above:
        protected abstract object Operation(object v1, object v2);

        FOp L { get; }
        FOp R { get; }
    }

    public class FBinOpArith<T> : FBinOp<T>
    {
        public FBinOpArith(FOp left, FOp right, Func<double, double, double> func, String? name = null) : base(left, right, name)
        {
            this.Func = func;
        }

        public Func<double, double, double> Func { get; }

        protected override object Operation(object v1, object v2)
        { // TODO: Ivan's ValueHandler probably knows best.
            return Func(v1 is double ? (double)v1 : Double.Parse(v1.ToString()), v2 is double ? (double)v2 : Double.Parse(v2.ToString()));
        }
    }

    public class FAcc<T> : FOp where T : INumber<T>
    {
        // We accumulate the values of a property by overriding `Operation`.
        // Construct "fake" property to hold accumulator value derived from the object id(!), not the name.
        // The "cool" hack is that you can also use an FOp(-result) as input.

        public FAcc(FOp op, String? name = null)
        {
            this.Op = op;
            this.Orig = op.Prop;
            this.Prop = new Property() { OwlType = op.Prop.OwlType, Name = name ?? GetHashCode().ToString() + "_ACC", Value = null };
        }

        internal override IEnumerable<object> MkInitialValues(Simulation s)
        {
            return new[] { s.PropertyCache.Properties.ContainsKey(Prop.Name) ? s.PropertyCache.Properties[Prop.Name].Value : 0.0 }.Concat(Op.MkInitialValues(s));
        }

        internal override IEnumerable<Property> MkProps()
        {
            return new[] { Prop }.Concat(Op.MkProps());
        }

        internal override void Eval(AccState in_state, Simulation sim, AccState out_state)
        {
            Op.Eval(in_state, sim, out_state);
            out_state.Set(Prop, (T)in_state.Get(Prop) + (T)out_state.Get(Orig));
        }

        Property Orig { get; }

        private readonly FOp Op;
    }

    public class FRelOp<T> : FBinOp<bool> where T : INumber<T>
    {
        public FRelOp(FOp left, FOp right, Func<T, T, bool> func, String? name = null) : base(left, right, name)
        {
            this.Func = func;
        }

        public Func<T, T, bool> Func { get; }

        protected override object Operation(object v1, object v2)
        {
            return Func(v1 is T ? (T)v1 : T.Parse(v1.ToString(), CultureInfo.InvariantCulture), v2 is T ? (T)v2 : T.Parse(v2.ToString(), CultureInfo.InvariantCulture));
        }
    }

    public class FITE<T> : FOp
    {
        public FITE(FOp condition, FOp thenBranch, FOp elseBranch, String? name = null)
        {
            this.Condition = condition;
            this.ThenBranch = thenBranch;
            this.ElseBranch = elseBranch;
            this.Prop = new Property() { OwlType = thenBranch.Prop.OwlType, Name = name ?? GetHashCode().ToString() + "_ITE", Value = null };
        }

        internal override IEnumerable<object> MkInitialValues(Simulation s)
        {
            var c = Condition.MkInitialValues(s);
            var t = ThenBranch.MkInitialValues(s);
            var e = ElseBranch.MkInitialValues(s);
            return new object[]
            { null }.Concat(c).Concat(t).Concat(e);
        }

        internal override IEnumerable<Property> MkProps()
        {
            return new[] { Prop }.Concat(Condition.MkProps()).Concat(ThenBranch.MkProps()).Concat(ElseBranch.MkProps());
        }

        internal override void Eval(AccState in_state, Simulation sim, AccState out_state)
        {
            Condition.Eval(in_state, sim, out_state);
            bool condValue = (bool)in_state.Get(Condition.Prop);
            if (condValue)
            {
                ThenBranch.Eval(in_state, sim, out_state);
                out_state.Set(Prop, in_state.Get(ThenBranch.Prop));
            }
            else
            {
                ElseBranch.Eval(in_state, sim, out_state);
                out_state.Set(Prop, in_state.Get(ElseBranch.Prop));
            }
        }

        FOp Condition { get; }
        FOp ThenBranch { get; }
        FOp ElseBranch { get; }
    }

    public class FConst<T> : FOp
    {
        public FConst(T value, string type)
        {
            this.Value = value;
            this.Prop = new Property() { OwlType = type, Name = GetHashCode().ToString() + "_CONST", Value = value };
        }

        internal override IEnumerable<object> MkInitialValues(Simulation s)
        {
            return new[] { Value };
        }

        internal override IEnumerable<Property> MkProps()
        {
            return new[] { Prop };
        }

        internal override void Eval(AccState in_state, Simulation sim, AccState out_state)
        {
            out_state.Set(Prop, Value);
        }

        public object Value { get; }
    }
}