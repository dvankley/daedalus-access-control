/* 
 * File:   main.cpp
 * Author: Dan
 *
 * Created on April 9, 2012, 8:50 PM
 */

#include <cstdlib>
#include <iostream>
#include <boost/lambda/lambda.hpp>

#include "dio.h"
#include "sbus.h"

using namespace std;

/*
 * 
 */
int main(int argc, char** argv) {
    unsigned int usecs = 2000000; 
    
    cout << "Hi mom!";
    usleep(usecs);
    
    sbuslock();
    setdiopin(RELAY_1, RELAY_ACTIVE);
    sbusunlock();
    usleep(usecs);
    
    sbuslock();
    setdiopin(RELAY_2, RELAY_ACTIVE);
    sbusunlock();
    usleep(usecs);
    
    sbuslock();
    setdiopin(RELAY_3, RELAY_ACTIVE);
    sbusunlock();
    usleep(usecs);
    
    
    sbuslock();
    setdiopin(RELAY_1, RELAY_INACTIVE);
    sbusunlock();
    usleep(usecs);
    
    sbuslock();
    setdiopin(RELAY_2, RELAY_INACTIVE);
    sbusunlock();
    usleep(usecs);
    
    sbuslock();
    setdiopin(RELAY_3, RELAY_INACTIVE);
    sbusunlock();
    usleep(usecs);    
    
    return 0;
}

