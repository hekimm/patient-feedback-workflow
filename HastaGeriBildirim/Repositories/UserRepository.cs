using System.Data;
using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.Entities;

namespace HastaGeriBildirim.Repositories;

public class UserRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public UserRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                u.USER_ID as UserId,
                u.USERNAME as Username,
                u.FULL_NAME as FullName,
                u.EMAIL as Email,
                u.PASSWORD_HASH as PasswordHash,
                r.ROLE_CODE as RoleCode,
                CASE WHEN u.STATUS = 'ACTIVE' THEN 1 ELSE 0 END as IsActive,
                u.CREATED_AT as CreatedAt
            FROM HGB_USERS u
            LEFT JOIN HGB_ROLES r ON u.PRIMARY_ROLE_ID = r.ROLE_ID
            WHERE u.USERNAME = :Username AND u.STATUS = 'ACTIVE'";

        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                u.USER_ID as UserId,
                u.USERNAME as Username,
                u.FULL_NAME as FullName,
                u.EMAIL as Email,
                r.ROLE_CODE as RoleCode,
                CASE WHEN u.STATUS = 'ACTIVE' THEN 1 ELSE 0 END as IsActive,
                u.CREATED_AT as CreatedAt
            FROM HGB_USERS u
            LEFT JOIN HGB_ROLES r ON u.PRIMARY_ROLE_ID = r.ROLE_ID
            ORDER BY u.FULL_NAME";

        var results = await connection.QueryAsync<User>(sql);
        return results.ToList();
    }

    public class RoleInfo
    {
        public int RoleId { get; set; }
        public string RoleCode { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public async Task<List<RoleInfo>> GetRolesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                ROLE_ID as RoleId,
                ROLE_CODE as RoleCode,
                ROLE_NAME as RoleName,
                DESCRIPTION as Description
            FROM HGB_ROLES
            ORDER BY ROLE_ID";

        var results = await connection.QueryAsync<RoleInfo>(sql);
        return results.ToList();
    }

    public class RolePermissionRow
    {
        public string RoleCode { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string PermissionName { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
    }

    public async Task<List<RolePermissionRow>> GetRolePermissionsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                r.ROLE_CODE as RoleCode,
                r.ROLE_NAME as RoleName,
                p.PERMISSION_NAME as PermissionName,
                p.MODULE_NAME as ModuleName
            FROM HGB_ROLE_PERMISSIONS rp
            JOIN HGB_ROLES r ON rp.ROLE_ID = r.ROLE_ID
            JOIN HGB_PERMISSIONS p ON rp.PERMISSION_ID = p.PERMISSION_ID
            ORDER BY r.ROLE_ID, p.MODULE_NAME";

        var results = await connection.QueryAsync<RolePermissionRow>(sql);
        return results.ToList();
    }

    public async Task<int> CreateUserAsync(
        string username, string fullName, string? email, string passwordHash, int roleId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO HGB_USERS
            (USERNAME, PASSWORD_HASH, FULL_NAME, EMAIL, PRIMARY_ROLE_ID, STATUS)
            VALUES
            (:Username, :PasswordHash, :FullName, :Email, :RoleId, 'ACTIVE')
            RETURNING USER_ID INTO :UserId";

        var parameters = new DynamicParameters();
        parameters.Add("Username", username);
        parameters.Add("PasswordHash", passwordHash);
        parameters.Add("FullName", fullName);
        parameters.Add("Email", email);
        parameters.Add("RoleId", roleId);
        parameters.Add("UserId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(sql, parameters);
        var userId = parameters.Get<int>("UserId");

        var roleSql = @"
            INSERT INTO HGB_USER_ROLES (USER_ID, ROLE_ID)
            VALUES (:UserId2, :RoleId2)";

        var roleParameters = new DynamicParameters();
        roleParameters.Add("UserId2", userId);
        roleParameters.Add("RoleId2", roleId);

        await connection.ExecuteAsync(roleSql, roleParameters);

        return userId;
    }

    public async Task SetUserStatusAsync(int userId, bool isActive)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_USERS
            SET STATUS = :Status, UPDATED_AT = SYSTIMESTAMP
            WHERE USER_ID = :UserId";

        var parameters = new DynamicParameters();
        parameters.Add("Status", isActive ? "ACTIVE" : "PASSIVE");
        parameters.Add("UserId", userId);

        await connection.ExecuteAsync(sql, parameters);
    }
}
