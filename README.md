
# TracerToCirc
TracerToCirc is a class that can be easily added to a qsharp project. As the name suggests, it listens to a quantum simulator and outputs the gates list in a cirq compatible format.

There are two directories. `TracerToCirq` contains cirq conversion code, configuration and helper functions. The other is the `ExecutionPathTracer` folder which contains code from Microsoft's iqsharp repository, modified for the purpose of realtime gate outputs. <https://github.com/microsoft/iqsharp/tree/main/src/ExecutionPathTracer>
This directory can be sourced directly from the iqsharp repository however some minor modifications may need to be made.

## Setup and Usage
1) Add both directories to your qsharp project directory.
2) Initialise a tracer
```C#
using static TracerToCirq;

var tracer = new ExecutionPathTracer();
```
3) Attach the tracer to your simulator
e.g. 
```C#
var qsim = new QuantumSimulator().WithExecutionPathTracer(tracer);
```
4) Call ToCirq after your simulator has run
```C#
ToCirq(tracer, "output_file_name");
```

TracerToCirq will go through the execution path taken and build a cirq representation of your project.

## Configuration
Optionally, add your own configuration by setting one or more flags like so.

```C#
ToCirq(tracer, "qsim_grover", 
	new TracerToCirqConfig{
	ThrowOnNoPrimitiveBreakdown = false,
	RenderUnhandledOperations = true,
	Debug = false, 
	Depth = 2,
	Jabalize = true
});
```

`ThrowOnNoPrimitiveBreakdown` - If set to true, an exception will be thrown if an operation is called and no primitive gate is called as a result.

`RenderUnhandledOperations` - If set to true, when an operation does not call any primitive gates like above, the gate will be inserted as a comment in the output file. 

`Debug` - The gate will be printed in console as they are processed, reserved for debugging.

`Depth` - Similar to iqsharps functionality, this will group the outputs based on how deep they are in the execution stack. Setting to 0 will output each gate on the same line.

`Jabalize` - Setting to true will output the circuit in a form ready for Jabalizer.


## Realtime
Realtime processing can be demo'd by changing the realtime flag to true in Execution



## Unsupported/TODO
 - MResetX/Y/Z doesn't work currently 
 - The QuantumSimulator will optimise for some ops and not break them down. E.g. the Exp/ExpFrac operations have been manually added for this specific reason. There may be more instances of this occurring undiscovered.
 - Probably a lot more

	
