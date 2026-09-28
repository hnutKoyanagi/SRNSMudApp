// Service Worker for SRNS PWA & Web Push
const CACHE_NAME = 'srns-pwa-cache-v2';

// オフライン時に利用可能とする基本静的アセット
const PRECACHE_ASSETS = [
    '/manifest.json',
    '/favicon.ico',
    '/images/icons/icon-192x192.png',
    '/images/icons/icon-512x512.png',
    '/images/icons/icon-maskable-512x512.png'
];

// 1. インストール時: 事前キャッシュの実行
self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then((cache) => cache.addAll(PRECACHE_ASSETS))
            .then(() => self.skipWaiting())
    );
});

// 2. アクティベーション時: 古いキャッシュの削除
self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys().then((keys) => {
            return Promise.all(
                keys.map((key) => {
                    if (key !== CACHE_NAME) {
                        return caches.delete(key);
                    }
                })
            );
        }).then(() => self.clients.claim())
    );
});

// 3. フェッチ時: キャッシュ優先 (APIリクエストやBlazor WebSocketは除外)
self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);

    // APIリクエスト、Blazor ServerのSignalR/WebSocket (_blazor)、認証エンドポイントはキャッシュしない
    if (event.request.method !== 'GET' ||
        url.pathname.startsWith('/api/') ||
        url.pathname.startsWith('/_blazor') ||
        url.pathname.startsWith('/Account/')) {
        return;
    }

    // ナビゲーション（HTMLページ遷移）は常にネットワーク優先 (Network-First)
    // Blazor Serverの動的SSRや認証状態、サーキット初期化トークンを最新の状態で取得するため
    if (event.request.mode === 'navigate') {
        event.respondWith(
            fetch(event.request).catch(() => caches.match(event.request))
        );
        return;
    }

    event.respondWith(
        caches.match(event.request).then((cachedResponse) => {
            if (cachedResponse) {
                return cachedResponse;
            }
            return fetch(event.request);
        })
    );
});

// 4. push イベント: バックエンドからのプッシュ通知を受信してシステム通知を表示
self.addEventListener('push', (event) => {
    let payload = {
        title: '新着通知',
        body: '新しいメッセージが届きました。',
        icon: '/images/icons/icon-192x192.png',
        url: '/notifications'
    };

    if (event.data) {
        try {
            payload = Object.assign(payload, event.data.json());
        } catch (e) {
            payload.body = event.data.text();
        }
    }

    const title = payload.title || 'SRNS 通知';
    const options = {
        body: payload.body,
        icon: payload.icon || '/images/icons/icon-192x192.png',
        badge: '/images/icons/icon-192x192.png',
        data: {
            url: payload.url || '/notifications'
        },
        vibrate: [100, 50, 100]
    };

    event.waitUntil(
        self.registration.showNotification(title, options)
    );
});

// 5. notificationclick イベント: 通知タップ時に指定URLへフォーカスまたは遷移
self.addEventListener('notificationclick', (event) => {
    event.notification.close();

    const targetUrl = event.notification.data?.url || '/notifications';

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then((windowClients) => {
            for (const client of windowClients) {
                if (client.url.includes(targetUrl) && 'focus' in client) {
                    return client.focus();
                }
            }
            if (clients.openWindow) {
                return clients.openWindow(targetUrl);
            }
        })
    );
});
