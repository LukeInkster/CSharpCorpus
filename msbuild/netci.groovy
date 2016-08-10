// Import the utility functionality.
import jobs.generation.Utilities;

// Defines a the new of the repo, used elsewhere in the file
def project = GithubProject

// Generate the builds for branches: xplat, master and PRs (which aren't branch specific)
['*/master', '*/xplat', 'pr'].each { branch ->
    ['Windows_NT', 'OSX', 'Ubuntu'].each {osName ->
        def runtimes = ['CoreCLR']

        if (osName == 'Windows_NT') {
            runtimes.add('Desktop')
        }

        // TODO: Mono

        runtimes.each { runtime ->
            def isPR = false
            def newJobName = ''
            def skipTestsWhenResultsNotFound = true

            if (branch == 'pr') {
                isPR = true
                newJobName = Utilities.getFullJobName(project, "_${osName}_${runtime}", isPR)
            } else {
                newJobName = Utilities.getFullJobName(project, "innerloop_${branch.substring(2)}_${osName}_${runtime}", isPR)
            }

            // Create a new job with the specified name.  The brace opens a new closure
            // and calls made within that closure apply to the newly created job.
            def newJob = job(newJobName) {
                description('')
            }

            // Define job.
            switch(osName) {
                case 'Windows_NT':
                    newJob.with{
                        steps{
                            batchFile("call \"C:\\Program Files (x86)\\Microsoft Visual Studio 14.0\\VC\\vcvarsall.bat\" && cibuild.cmd --target ${runtime}")
                        }

                        skipTestsWhenResultsNotFound = false
                    }

                    break;
                case 'OSX':
                    newJob.with{
                        steps{
                            shell("./cibuild.sh --scope Test --target ${runtime}")
                        }
                    }

                    break;
                case 'Ubuntu':
                    // Do not run tests on Ubuntu. We don't yet have a green test baseline.
                    newJob.with{
                        steps{
                            shell("./cibuild.sh --scope Compile --target ${runtime}")
                        }
                    }

                    break;
            }

            // Add xunit result archiving. Skip if no results found.
            Utilities.addXUnitDotNETResults(newJob, 'bin/**/*_TestResults.xml', skipTestsWhenResultsNotFound)
            Utilities.setMachineAffinity(newJob, osName, 'latest-or-auto')
            Utilities.standardJobSetup(newJob, project, isPR, branch)
            // Add archiving of logs (even if the build failed)
            Utilities.addArchival(newJob,
                                  'msbuild*.log,**/Microsoft.*.UnitTests.dll_*', /* filesToArchive */
                                  '', /* filesToExclude */
                                  false, /* doNotFailIfNothingArchived */
                                  false, /* archiveOnlyIfSuccessful */)
            // Add trigger
            if (isPR) {
                Utilities.addGithubPRTrigger(newJob, "${osName} Build for ${runtime}")
            } else {
                Utilities.addGithubPushTrigger(newJob)
            }
        }
    }
}
