package cps_code_generation.inference_rules;

import java.io.FileNotFoundException;
import java.io.FileOutputStream;
import java.util.Iterator;
import java.util.List;

import org.apache.jena.rdf.model.*;
import org.apache.jena.reasoner.Reasoner;
import org.apache.jena.reasoner.ReasonerRegistry;
import org.apache.jena.reasoner.ValidityReport;
import org.apache.jena.reasoner.rulesys.GenericRuleReasoner;
import org.apache.jena.reasoner.rulesys.Rule;
import org.apache.jena.riot.Lang;
import org.apache.jena.riot.RDFDataMgr;

public class App 
{	
    public static void main( String[] args ) throws FileNotFoundException
    {
    	String ontologyFilePath = args[0];
    	String instanceModelFilePath = args[1];
    	String ruleModelFilePath = args[2];
    	String inferredModelFilePath = args[3];
    	
    	Model myOntology = RDFDataMgr.loadModel(ontologyFilePath);
    	Model instanceModel = RDFDataMgr.loadModel(instanceModelFilePath);
    	
    	Reasoner owlReasoner = ReasonerRegistry.getOWLReasoner()
    			.bindSchema(myOntology);
    	InfModel basicInferredModel = ModelFactory.createInfModel(owlReasoner, instanceModel);
    	
    	List ruleList = Rule.rulesFromURL(ruleModelFilePath);
    	GenericRuleReasoner ruleReasoner = new GenericRuleReasoner(ruleList);
    	InfModel finalInferredModel = ModelFactory.createInfModel(ruleReasoner, basicInferredModel);
    	
    	ValidityReport validityReport = finalInferredModel.validate();
    	if (validityReport.isValid()) {
    	    System.out.println("The model is valid.");
    	} else {
    	    System.out.println("Inconsistencies detected!");
    	    for (Iterator i = validityReport.getReports(); i.hasNext(); ) {
    	        System.out.println(" - " + i.next());
    	    }
    	}
    	
    	FileOutputStream fileOutputStream = new FileOutputStream(inferredModelFilePath);
    	RDFDataMgr.write(fileOutputStream, finalInferredModel, Lang.TTL);
    	
    	System.out.println("Terminated.");
    }
}
