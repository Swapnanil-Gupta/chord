namespace ConfigModule

open Akka.Configuration

module ConfigModule =
    // config to count total number of requests in the system
    let mutable completionThreshold = 0 
    // interval tp run stabilization of the ring
    let stabilizationInterval = 100.0
    // interval to run finger table updates
    let fingerTableUpdateInterval = 300.0
    // akka config
    let akkaConfiguration = 
        ConfigurationFactory.ParseString(
            @"akka {            
                stdout-loglevel : ERROR
                loglevel : ERROR
                log-dead-letters = 0
                log-dead-letters-during-shutdown = off
            }")