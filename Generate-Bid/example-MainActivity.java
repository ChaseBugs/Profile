package com.yourcompany.yourapp;

import android.os.Bundle;
import com.getcapacitor.BridgeActivity;
import com.getcapacitor.Plugin;

import java.util.ArrayList;

public class MainActivity extends BridgeActivity {
    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        
        // Initialize Capacitor with plugins
        // Add your plugin classes here if you have custom plugins
        this.init(savedInstanceState, new ArrayList<Class<? extends Plugin>>() {{
            // Example: add(YourCustomPlugin.class);
        }});
    }
}

