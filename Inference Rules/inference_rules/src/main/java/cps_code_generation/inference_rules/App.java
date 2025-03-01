package cps_code_generation.inference_rules;

import java.io.FileNotFoundException;
import java.io.FileOutputStream;
import java.util.List;

import org.apache.jena.rdf.model.*;
import org.apache.jena.reasoner.Reasoner;
import org.apache.jena.reasoner.ReasonerRegistry;
import org.apache.jena.reasoner.rulesys.GenericRuleReasoner;
import org.apache.jena.reasoner.rulesys.Rule;
import org.apache.jena.riot.Lang;
import org.apache.jena.riot.RDFDataMgr;

public class App 
{
	private static final String ontologyBasePath = "C:\\dev\\cps-code-generation\\Ontology\\";
	private static final String instanceBasePath = "C:\\dev\\cps-code-generation\\Instance Models\\";
	
    public static void main( String[] args ) throws FileNotFoundException
    {
    	// Load the ontology (meta-model) and instance, and set up an OWL reasoner to use the ontology as a
    	// schema for the instance model.
    	Model ontology = RDFDataMgr.loadModel(ontologyBasePath + "cps-code-generation.ttl");
    	Model instanceModel = RDFDataMgr.loadModel(instanceBasePath + "instance-model-1.ttl");
    	Reasoner owlReasoner = ReasonerRegistry.getOWLReasoner()
    			.bindSchema(ontology);
    	
    	// Infer the preliminary inferred model from basic OWL rules.
    	InfModel basicInferredModel = ModelFactory.createInfModel(owlReasoner, instanceModel);
    	
    	// Load the list of custom inference rules, instantiate a generic rule reasoner, and infer the final
    	// model.
    	List ruleList = Rule.rulesFromURL(instanceBasePath + "instance-model-1-inference-rules.rules");
    	GenericRuleReasoner ruleReasoner = new GenericRuleReasoner(ruleList);
    	InfModel finalInferredModel = ModelFactory.createInfModel(ruleReasoner, basicInferredModel);
    	
    	FileOutputStream fileOutputStream = new FileOutputStream(instanceBasePath + "inferred-model-1.ttl");
    	RDFDataMgr.write(fileOutputStream, finalInferredModel, Lang.TTL);
    }
}
