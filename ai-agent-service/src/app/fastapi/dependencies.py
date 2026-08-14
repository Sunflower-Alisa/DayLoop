from __future__ import annotations

from dataclasses import dataclass

from fastapi import Header, HTTPException

from app.core.exceptions import APIError


@dataclass
class AuthContext:
    user_id: str
    role: str = "user"


def require_auth(x_user_id: str = Header(default="", alias="X-User-Id")) -> AuthContext:
    """从请求头 X-User-Id 提取用户标识（MVP：不做签名校验）。"""
    user_id = (x_user_id or "").strip()
    if not user_id:
        raise HTTPException(status_code=401, detail="缺少 X-User-Id 请求头")
    return AuthContext(user_id=user_id)


def parse_user_id(token: str) -> AuthContext:
    """面向服务端内部调用的鉴权解析（保留接口，方便后续扩展 JWT）。"""
    if not token:
        raise APIError("认证 token 为空")
    return AuthContext(user_id=token)