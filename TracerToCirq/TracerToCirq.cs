using Microsoft.Quantum.IQSharp.ExecutionPathTracer;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;


/// <summary>
/// The configuration of <see cref="TracerToCirq"/>. 
/// </summary>
[Serializable]
public class TracerToCirqConfig
{
    /// <summary>
    /// If set to true, the cirq output will throw an exception when there is no primitive gate breakdown for an operation.
    /// This can occur when an operation is called but does not execute a gate on any qubits
    /// </summary>
    public bool ThrowOnNoPrimitiveBreakdown = false;

    /// <summary>
    /// If set to true, the cirq output will output a commented out line where the operation takes place.
    /// This can occur when an operation is called but does not execute a gate on any qubits
    /// </summary>
    public bool RenderUnhandledOperations = true;

    /// <summary>
    /// The cirq output will print a new line for each gate called at this depth or higher in the execution tree
    /// </summary>
    public int Depth = 2;

    /// <summary>
    /// If set to true, the gates will be printed in console as they are processed.
    /// </summary>
    public bool Debug = false;

    /// <summary>
    /// If set to true, the output will be in a format ready for Jabalizer
    /// </summary>
    public bool Jabalize = false;      
}


public static class TracerToCirq{
	private static Dictionary<string,int> supportedGateList = new Dictionary<string, int> {	
	{"X",0}, 
	{"Y",0}, 
	{"Z",0},
	{"H",0}, 
	{"S",0}, 
	{"T",0},
	{"CZ",0}, 
	{"CNOT",0}, 
	{"SWAP",0}, 
	{"CCNOT",0}, 
	{"CCX",0}, 
	{"CCZ",0},
	{"Interface_Clifford",1}, 
	{"Interface_CX",2},
	{"Interface_RFrac",3}, 
	{"Interface_R",3}, 
	{"R",3},
	{"M",4},
	{"Measure",4},
	{"Reset",5},
	{"NoOp", 6}
	};

	private static int measurementKey;

	private static List<string> lines;
	private static List<string> gatesUsed;
	private static List<string> gatesErrored;
	private static int cursor;
	private static List<int> levels;
	private static TracerToCirqConfig config;
	private static bool realtime = false;

	public static void SetRealTime(bool real){
		realtime = real;
	}

	public static void ToCirq(ExecutionPathTracer tracer, string fileName) { 
		ToCirq(tracer, fileName, new TracerToCirqConfig());
	}

	public static void ToCirq(ExecutionPathTracer tracer, string fileName, TracerToCirqConfig configuration) {
		config = configuration;
		measurementKey = 0;
		cursor = 0;
		lines = new List<string>();
		levels = new List<int>();
		gatesUsed = new List<string>();
		gatesErrored = new List<string>();

		ExecutionPath xPath = tracer.GetExecutionPath();


		foreach (Operation op in xPath.Operations){
			Recursion(op, 0);

		}

		if (config.Jabalize){
			System.Console.WriteLine("What's Jablin Jables?");
			JabalizeOut((xPath.Qubits.Count()).ToString(), fileName);
		} else {
			PythonOut((xPath.Qubits.Count()).ToString(), fileName);
		}
		

	}


	public static void PythonOut(string num_qubits, string fileName) {
		using (System.IO.StreamWriter file = new System.IO.StreamWriter(fileName + ".py")){
			file.WriteLine("# Qubits: " + num_qubits);
			if (gatesErrored.Count > 0)
			{
				file.WriteLine("# Operations that did not call a primitive gate: " + string.Join(", ", gatesErrored));
			}

			file.WriteLine(@"
import cirq
import numpy as np");

			file.WriteLine("from cirq.ops import " + string.Join(", ", gatesUsed));
			file.WriteLine("qubits = cirq.LineQubit.range(" + num_qubits + ")");
			file.WriteLine("circuit = cirq.Circuit()");

			List<string> tempLines = new List<string>();

			for (var i = 0; i < lines.Count; i++) {
				if ((i!=0) && (levels[i]!=levels[i-1])){
					file.WriteLine(cleanErrors(tempLines, "circuit"));
					tempLines = new List<string>();
				}
				tempLines.Add(lines[i]);
			}

			if (tempLines.Count != 0){
				file.WriteLine(cleanErrors(tempLines, "circuit"));
			}

		}
		System.Console.WriteLine("Translation complete: " + fileName + ".py");		
	}


	public static void JabalizeOut(string num_qubits, string fileName) {
		using (System.IO.StreamWriter file = new System.IO.StreamWriter(fileName + ".py")){
			if (gatesErrored.Count > 0)
			{
				file.WriteLine("# Operations that did not call a primitive gate: " + string.Join(", ", gatesErrored));
			}

			file.WriteLine(@"# Example of how to definite a cirq circuit
# main code should import cirq
def build_circuit():
    '''Function that builds the cirq circuit'''
    import numpy as np

    from cirq import GridQubit, Circuit
    from cirq.circuits import InsertStrategy as strategy");

			file.WriteLine("    from cirq.ops import " + string.Join(", ", gatesUsed));

			file.WriteLine(@"
    # initialise qubits
    qubits = [GridQubit(i, 0) for i in range(" + num_qubits + @")]

    # initialise moments (veritcal slices)
    moments = []

    # add vertical slices");

			List<string> tempLines = new List<string>();

			for (var i = 0; i < lines.Count; i++) {
				if ((i!=0) && (levels[i]!=levels[i-1])){
					file.WriteLine("    " + cleanErrors(tempLines, "moments"));
					tempLines = new List<string>();
				}
				tempLines.Add(lines[i]);
			}

			if (tempLines.Count != 0){
				file.WriteLine("    " + cleanErrors(tempLines, "moments"));
			}


			file.WriteLine(@"
    circuit = Circuit()

    # cirq will flush gates left, the strategy argument given will
    #prevent this. If this is not desired remove the strategy argument.
    for moment in moments:
        circuit.append(moment, strategy=strategy.NEW_THEN_INLINE)

    return circuit

    # # Convert moments to a circuit and return it
    # return cirq.Circuit(moments)

if __name__ == '__main__':
    ''' This bit will only run if this file is executed.
    This allows you to import the function to other files ignoring
    whatever is below. This part isn't needed for the gui '''


    circuit = build_circuit()
    print(circuit)");



		}
		System.Console.WriteLine("Translation complete: " + fileName + ".py");		
	}

	private static string cleanErrors(List<string> tempLines, string arrayName){
		string s;
		if (tempLines[0].StartsWith('#')){
			s = tempLines[0];
		} else {
			s = arrayName + ".append([" +  string.Join(", ", tempLines) + "])";
		}	
		return s;
	}


	private static void Recursion (Operation op, int level){
		int currentLevel = level + 1;
		if (currentLevel <= config.Depth){
			cursor++;
		}
		if (config.Debug){
			string spacing = new string(' ', currentLevel * 2);
			System.Console.WriteLine(spacing + op.Gate);
			System.Console.WriteLine(spacing + op.DisplayArgs);			
		}
		if (supportedGateList.ContainsKey(op.Gate)) {
			Parse(op, false);

		} else if (op.Children != null)
        {
			//System.Console.WriteLine(spacing + op.Gate);
			foreach (Operation child in op.Children) {
				Recursion(child, currentLevel);

			}
		} else if (op.Gate == "PauliZFlip"){
			// PauliZFlip is an example of an op that will get called without calling any primitive gates for some arguments

		} else if (op.Gate == "Exp"){ // Exp will get called without calling any primitive gates if using qsim, hence we must emulate the rest of the circuit tree
			Exp(op);
		} else {
			if (!gatesErrored.Contains(op.Gate)){
				gatesErrored.Add(op.Gate);
			}
			if (config.ThrowOnNoPrimitiveBreakdown) {
				throw new Exception("Operation " + op.Gate + " does not call any primitve gate using this simulator. (Try QCTraceSimulator?)");
			} else if (config.RenderUnhandledOperations){
				var (qubits, controls) = GetTargets(op, true);
				AddOp("#" + op.Gate, qubits, controls);
			}
			//

		}

	}


	private static (List<int>, List<int>) GetTargets(Operation op, bool controlFlag){
		List<int> controls = new List<int>();

		if (controlFlag) {
			foreach (Register qubit in op.Controls)
	        {
				if (!controls.Contains(qubit.QId))
				{
					controls.Add(qubit.QId);
				}
			}

		}


		List<int> qubits = new List<int>();
		foreach (Register qubit in op.Targets)
        {
			if ((!qubits.Contains(qubit.QId)) && (!controls.Contains(qubit.QId)))
			{
				qubits.Add(qubit.QId);
			}
        }

        return (qubits, controls);
	}

	public static void Parse (Operation op, bool real){
		realtime = real;
		var opcode = supportedGateList[op.Gate];
		if ((realtime) || (config.Debug)){
			System.Console.WriteLine(opcode);
		}
		var (qubits, controls) = GetTargets(op, true);

		switch (opcode) {
			case 0: // No processing needed
				AddOp(op.Gate, qubits, controls);
				break;
			case 1:
				ProcClifford(op.DisplayArgs, qubits, controls);
				break;
			case 2:
				AddOp("CX", qubits, new List<int>());
				break;
			case 3: // Rotational Gates
				AddRotationOp(op.DisplayArgs, qubits[0], controls);
				break;
			case 4: // Measurements
				(qubits, _) = GetTargets(op, false);
				Debug.Assert(qubits.Count == 1, "Measurement op on multiple qubits, expecting one");
				AddMeasure(qubits[0], new List<int>());
				break;
			case 5: // Reset
				(qubits, _) = GetTargets(op, false);
				AddOp("reset", qubits, new List<int>());
				break;
			case 6: // No Operation
				break;
		}
	}

	private static void AddOp(string symbol, List<int> qubits, List<int> controls){
		WriteLine(symbol + "(qubits[" + string.Join("], qubits[", qubits) + "])", symbol, controls);
	}

	private static void ProcClifford(string metadata, List<int> qubits, List<int> controls){
		var split = metadata.Trim('(', ')').Split(", ");
		Debug.Assert(qubits.Count == 1, "Clifford op on multiple qubits when expecting one");
		switch (split[1]){
			case "PauliX":
				AddOp("X", qubits, controls);
				break;
			case "PauliY":
				AddOp("Y", qubits, controls);
				break;
			case "PauliZ":
				AddOp("Z", qubits, controls);
				break;			
		}

		switch (split[0]){
			case "1":
				AddOp("H", qubits, controls);
				break;
			case "2":
				AddOp("S", qubits, controls);
				break;
			case "3":
				AddOp("H", qubits, controls);
				AddOp("S", qubits, controls);
				break;
			case "4":
				AddOp("S", qubits, controls);
				AddOp("H", qubits, controls);
				break;
			case "5":
				AddOp("H", qubits, controls);
				AddOp("S", qubits, controls);
				AddOp("H", qubits, controls);
				break;
		}


	}

	private static void AddRotationOp(string metadata, int qubit, List<int> controls){
		var split = metadata.Trim('(',')').Split(", ");

		string symbol = ("r" + split[0].Last()).ToLower();
		if (("" + split[0].Last()).ToLower() == "i"){
			symbol = "#"+ symbol;
		}
		string angle;
		if (split.Length == 2){
			angle = split[1];
		} else {
			angle = "-np.pi*" + split[1] + "/(2**(" + split[2] + "-1))";
		}
		WriteLine(symbol + "(rads=" + angle + ").on(qubits[" + qubit + "])", symbol, controls);
	}

	private static void AddMeasure (int qubit, List<int> controls){
		WriteLine("measure(qubits[" + qubit + "], key='" + measurementKey + "')", "measure", controls);
		measurementKey++;
	}

	private static void WriteLine(string line, string symbol, List<int> controls)
	{
		if (line.StartsWith('#')){
			cursor++;
		}
		if (controls.Count != 0){
			if (realtime){
				System.Console.WriteLine(line + ".controlled_by(qubits[" + string.Join("], qubits[", controls) + "])");
				return;
			} else {
				lines.Add(line + ".controlled_by(qubits[" + string.Join("], qubits[", controls) + "])");
			}
		} else {
			if (realtime){
				System.Console.WriteLine(line);
				return;
			} else {
				lines.Add(line);
			}
		}
		levels.Add(cursor);
		if (line.StartsWith('#')){
			cursor++;
		} else if (!gatesUsed.Contains(symbol)){
			gatesUsed.Add(symbol);
		}
	}


	public static void Exp(Operation op){
		var (qubits, _) = GetTargets(op, false);
		string[] split = op.DisplayArgs.Trim('(', ')', '[').Split("], ");
		string[] basis = split[0].Split(", ");
		string angle = split[1];
		

		List<int> nonIdentityIndicies  = new List<int>();
		List<string> nonIdentityPaulis  = new List<string>();
		int i = 0;
		foreach (int qubit in qubits) {
			if (basis[i] != "PauliI") {
				nonIdentityIndicies.Add(qubit);
				nonIdentityPaulis.Add(basis[i]);
			}
			i++;
		}		


		if (nonIdentityIndicies.Count == 0) {
			AddRotationOp("PauliI, " + "-2.0*" + angle, qubits[0], new List<int>());
		} else {
			//MultiPauliFlip(Pauli, target, 0)
			int q = 0;
			foreach (string p in nonIdentityPaulis) {

				if (p == "PauliX"){
					ProcClifford("(1, PauliI)", new List<int> { qubits[q] }, new List<int>());	//Interface_Clifford(1, PauliI, target);
				} else if (p == "PauliY"){
					ProcClifford("(4, PauliZ)", new List<int> { qubits[q] }, new List<int>());	//Interface_Clifford(4, PauliZ, target);
				}
				q++;
			}

			int k = nonIdentityIndicies[0];
			foreach (int j in nonIdentityIndicies) {
				if (j != k){
					AddOp("CX", new List<int> { qubits[nonIdentityIndicies[j]], qubits[k] }, new List<int>()); //InternalCX(j, k)						
				}
			}

			AddRotationOp("PauliZ, " + "-2.0*" + angle, qubits[0], new List<int>());

			for (int j = nonIdentityIndicies.Count-1; j>=0; j--) {
				if (j != k){
					AddOp("CX", new List<int> { qubits[j], qubits[k] }, new List<int>()); //InternalCX(j, k)						
				}
			}

			for (int p = nonIdentityPaulis.Count-1; p>=0; p--) {

				if (nonIdentityPaulis[p] == "PauliX"){
					ProcClifford("(1, PauliI)", new List<int> { qubits[p] }, new List<int>());	//Interface_Clifford(1, PauliI, target);
				} else if (nonIdentityPaulis[p] == "PauliY"){
					ProcClifford("(3, PauliI)", new List<int> { qubits[p] }, new List<int>());	//Interface_Clifford(4, PauliZ, target);
				}
			}

		}

	}

}



