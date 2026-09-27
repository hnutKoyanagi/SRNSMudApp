// Push Notification Client Interop

/**
 * Base64 URLエンコードされたVAPID公開鍵をUint8Arrayバイナリに変換するヘルパー
 */
function urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
    const base64 = (base64String + padding)
        .replace(/-/g, '+')
        .replace(/_/g, '/');

    const rawData = window.atob(base64);
    const outputArray = new Uint8Array(rawData.length);

    for (let i = 0; i < rawData.length; ++i) {
        outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
}

window.PushNotificationInterop = {
    /**
     * Service Worker を登録する
     */
    async registerServiceWorker() {
        if (!('serviceWorker' in navigator)) {
            console.warn('Service Worker is not supported by this browser.');
            return false;
        }

        try {
            await navigator.serviceWorker.register('/sw.js');
            return true;
        } catch (error) {
            console.error('Service Worker registration failed:', error);
            return false;
        }
    },

    /**
     * 現在のプッシュ通知の購読状況と権限を取得する
     */
    async getSubscriptionStatus() {
        if (!('Notification' in window) || !('serviceWorker' in navigator)) {
            return { supported: false, permission: 'denied', isSubscribed: false };
        }

        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();

        return {
            supported: true,
            permission: Notification.permission,
            isSubscribed: subscription !== null
        };
    },

    /**
     * プッシュ通知のパーミッション要求および購読処理を行い、バックエンドへ送信する
     * @param {string} vapidPublicKey - サーバーから提供されるVAPID公開鍵
     * @param {string} [userId] - ログインユーザーのID
     */
    async requestAndSubscribe(vapidPublicKey, userId) {
        if (!('Notification' in window) || !('serviceWorker' in navigator)) {
            return { success: false, error: 'Web Push is not supported on this browser.' };
        }

        try {
            // 1. パーミッションの確認と要求
            let permission = Notification.permission;
            if (permission === 'default') {
                permission = await Notification.requestPermission();
            }

            if (permission !== 'granted') {
                return { success: false, error: 'Notification permission was not granted: ' + permission };
            }

            // 2. Service Worker Ready 待機
            const registration = await navigator.serviceWorker.ready;

            // 3. 既存の購読確認、なければ新規購読
            let subscription = await registration.pushManager.getSubscription();
            if (!subscription) {
                const applicationServerKey = urlBase64ToUint8Array(vapidPublicKey);
                subscription = await registration.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: applicationServerKey
                });
            }

            // 4. サブスクリプション情報 (Endpoint, p256dh, auth) の抽出
            const subJson = subscription.toJSON();
            const payload = {
                endpoint: subJson.endpoint,
                keys: {
                    p256dh: subJson.keys.p256dh,
                    auth: subJson.keys.auth
                },
                userId: userId || null
            };

            // 5. バックエンドAPIへサブスクリプションを登録 (認証Cookieを含める)
            const response = await fetch('/api/pushnotification/subscribe', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                credentials: 'same-origin',
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                return { success: false, error: `Backend returned error code: ${response.status}` };
            }

            return { success: true, endpoint: payload.endpoint };
        } catch (error) {
            console.error('Push subscription failed:', error);
            return { success: false, error: error.message || 'Push subscription failed.' };
        }
    },

    /**
     * プッシュ通知の購読を解除する
     */
    async unsubscribe() {
        if (!('serviceWorker' in navigator)) {
            return { success: false, error: 'Service Worker not supported.' };
        }

        try {
            const registration = await navigator.serviceWorker.ready;
            const subscription = await registration.pushManager.getSubscription();
            if (subscription) {
                const endpoint = subscription.endpoint;
                await subscription.unsubscribe();

                // バックエンドからも削除
                await fetch('/api/pushnotification/unsubscribe', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    credentials: 'same-origin',
                    body: JSON.stringify({ endpoint: endpoint })
                });
            }
            return { success: true };
        } catch (error) {
            console.error('Push unsubscribe failed:', error);
            return { success: false, error: error.message || 'Unsubscribe failed.' };
        }
    },

    /**
     * 現在の端末に向けてテストプッシュ通知を要求する
     */
    async sendTestNotification() {
        try {
            const registration = await navigator.serviceWorker.ready;
            const subscription = await registration.pushManager.getSubscription();

            const response = await fetch('/api/pushnotification/test-me', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                credentials: 'same-origin',
                body: subscription ? JSON.stringify(subscription.toJSON()) : '{}'
            });

            const result = await response.json();
            return { success: response.ok, message: result.message };
        } catch (error) {
            return { success: false, message: error.message || 'テスト通知送信に失敗しました。' };
        }
    }
};

// ページ読み込み時に自動で Service Worker を登録
if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
        window.PushNotificationInterop.registerServiceWorker();
    });
}
