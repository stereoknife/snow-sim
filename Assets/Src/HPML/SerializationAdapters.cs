using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Serialization.Binary;

namespace HPML.Serialization
{
    public class doubleFAdapter : IBinaryAdapter<doubleF>
    {
        public doubleFAdapter(Allocator allocator)
        {
            _allocator = allocator;
        }

        private Allocator _allocator;
        
        public static int SizeOf(doubleF value) =>
            sizeof(int) * 2 // dimension
            + sizeof(double) * 3 // size
            + sizeof(int) // array length
            + sizeof(double) * value.Length; // array contents
        
        public unsafe void Serialize(in BinarySerializationContext<doubleF> context, doubleF value)
        {
            context.Writer->Add(value.dimension);
            context.Writer->Add(value.size);
            context.Writer->Add(value.field);
        }

        public unsafe doubleF Deserialize(in BinaryDeserializationContext<doubleF> context)
        {
            var dimension = context.Reader->ReadNext<int2>();
            var size = context.Reader->ReadNext<double3>();
            var arrayPtr = (double*)context.Reader->ReadNextArray<double>(out var length);
            var field = new doubleF(dimension, size, _allocator);
            UnsafeUtility.MemCpy(field.field.GetUnsafePtr(), arrayPtr, length * sizeof(double));
            return field;
        }
    }
    
    public class double2FAdapter : IBinaryAdapter<double2F>
    {
        public double2FAdapter(Allocator allocator)
        {
            _allocator = allocator;
        }

        private Allocator _allocator;
        
        public static int SizeOf(double2F value) =>
            sizeof(int) * 2 // dimension
            + sizeof(double) * 3 // size
            + sizeof(int) // array length
            + sizeof(double) * value.Length * 2; // array contents
        
        public unsafe void Serialize(in BinarySerializationContext<double2F> context, double2F value)
        {
            context.Writer->Add(value.dimension);
            context.Writer->Add(value.size);
            context.Writer->Add(value.array);
        }

        public unsafe double2F Deserialize(in BinaryDeserializationContext<double2F> context)
        {
            var dimension = context.Reader->ReadNext<int2>();
            var size = context.Reader->ReadNext<double3>();
            var arrayPtr = context.Reader->ReadNextArray<double2>(out var length);
            var field = new double2F(dimension, size, _allocator);
            UnsafeUtility.MemCpy(field.array.GetUnsafePtr(), arrayPtr, length * sizeof(double) * 2);
            return field;
        }
    }
    
    public class ScalarField2DAdapter : IBinaryAdapter<ScalarField2D>
    {
        public ScalarField2DAdapter(Allocator allocator)
        {
            _allocator = allocator;
        }

        private Allocator _allocator;
        
        public static int SizeOf(ScalarField2D value) =>
            sizeof(int) * 2 // dimension
            + sizeof(int) // layers
            + sizeof(double) * 2 // size
            + sizeof(int) // array length
            + sizeof(double) * value.Length * 2; // array contents
        
        public unsafe void Serialize(in BinarySerializationContext<ScalarField2D> context, ScalarField2D value)
        {
            context.Writer->Add(value.dimension);
            context.Writer->Add(value.layers);
            context.Writer->Add(value.size);
            context.Writer->Add(value.array);
        }

        public unsafe ScalarField2D Deserialize(in BinaryDeserializationContext<ScalarField2D> context)
        {
            var dimension = context.Reader->ReadNext<int2>();
            var layers = context.Reader->ReadNext<int>();
            var size = context.Reader->ReadNext<double2>();
            var arrayPtr = context.Reader->ReadNextArray<double>(out var length);
            var field = new ScalarField2D(dimension, layers, size, _allocator);
            UnsafeUtility.MemCpy(field.array.GetUnsafePtr(), arrayPtr, length * sizeof(double));
            return field;
        }
    }
}