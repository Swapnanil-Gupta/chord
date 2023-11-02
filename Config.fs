namespace ConfigModule

open Akka.Configuration

module ConfigModule =
    let mutable completionThreshold = 0 
    let stabilizationInterval = 100.0
    let fingerTableUpdateInterval = 300.0


    let akkaConfiguration = 
        ConfigurationFactory.ParseString(
            @"akka {            
                stdout-loglevel : ERROR
                loglevel : ERROR
                log-dead-letters = 0
                log-dead-letters-during-shutdown = off
            }")