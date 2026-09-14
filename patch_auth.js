--- SRNSMudApp/wwwroot/js/auth.js
+++ SRNSMudApp/wwwroot/js/auth.js
@@ -1,6 +1,13 @@
 window.customAuth = {
+    _isLoggingIn: false,
     async loginWithToken(provider, token) {
+        if (this._isLoggingIn) {
+            console.warn(`[AUTH JS] loginWithToken already in progress, ignoring duplicate call.`);
+            return;
+        }
+        this._isLoggingIn = true;
         console.log(`[AUTH JS] loginWithToken called with provider=${provider}, token=${token}`);
+        
         try {
             const response = await fetch('/api/auth/external-login', {
                 method: 'POST',
@@ -23,6 +30,9 @@
         } catch (error) {
             console.error('Login error:', error);
             alert('An error occurred during authentication.');
+        } finally {
+            // Allow subsequent attempts after a delay to prevent rapid consecutive executions
+            setTimeout(() => { this._isLoggingIn = false; }, 2000);
         }
     },
 
