FROM node:22-bullseye

# Install Java 17
RUN apt-get update && apt-get install -y openjdk-17-jdk wget unzip && rm -rf /var/lib/apt/lists/*

# Install Android SDK
ENV ANDROID_SDK_ROOT=/android-sdk
ENV ANDROID_HOME=/android-sdk
ENV PATH=$PATH:$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools

RUN mkdir -p $ANDROID_SDK_ROOT/cmdline-tools && \
    wget -q https://dl.google.com/android/repository/commandlinetools-linux-11076708_latest.zip -O /tmp/cmdline-tools.zip && \
    unzip -q /tmp/cmdline-tools.zip -d /tmp && \
    mv /tmp/cmdline-tools $ANDROID_SDK_ROOT/cmdline-tools/latest && \
    rm /tmp/cmdline-tools.zip && \
    yes | $ANDROID_SDK_ROOT/cmdline-tools/latest/bin/sdkmanager --licenses > /dev/null 2>&1 && \
    $ANDROID_SDK_ROOT/cmdline-tools/latest/bin/sdkmanager "platforms;android-34" "build-tools;34.0.0" "platform-tools"

WORKDIR /app

# Copy project files
COPY frontend/package*.json frontend/
COPY frontend/ ./frontend/
COPY backend/ ./backend/

# Install dependencies and build
WORKDIR /app/frontend
RUN npm install && npm run build

# Initialize Capacitor and build Android
RUN npm install @capacitor/core @capacitor/cli @capacitor/android && \
    npx cap init DayLoop com.dayloop.app --web-dir=dist && \
    npx cap add android && \
    npx cap copy android

WORKDIR /app/frontend/android
RUN ./gradlew assembleDebug

# Output the APK
RUN mkdir -p /app/output && \
    cp app/build/outputs/apk/debug/app-debug.apk /app/output/DayLoop.apk

CMD ["echo", "APK built at /app/output/DayLoop.apk"]
