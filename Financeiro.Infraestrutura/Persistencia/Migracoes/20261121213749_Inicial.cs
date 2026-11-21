using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Financeiro.Infraestrutura.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    firebase_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    escopo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categoria", x => x.id);
                    table.CheckConstraint("ck_categoria_escopo", "(escopo = 'PADRAO' AND usuario_id IS NULL) OR (escopo = 'PERSONALIZADA' AND usuario_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_categoria_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "codigo_vinculo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_e164 = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    codigo_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    usado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_codigo_vinculo", x => x.id);
                    table.ForeignKey(
                        name: "FK_codigo_vinculo_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conta_financeira",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    saldo_inicial = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false),
                    padrao = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conta_financeira", x => x.id);
                    table.UniqueConstraint("AK_conta_financeira_id_usuario_id", x => new { x.id, x.usuario_id });
                    table.CheckConstraint("ck_conta_padrao_ativa", "(padrao = false) OR (padrao = true AND ativa = true)");
                    table.ForeignKey(
                        name: "FK_conta_financeira_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contexto_consulta",
                columns: table => new
                {
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consulta = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    periodo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contexto_consulta", x => x.usuario_id);
                    table.ForeignKey(
                        name: "FK_contexto_consulta_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vinculo_whatsapp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_e164 = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vinculado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vinculo_whatsapp", x => x.id);
                    table.ForeignKey(
                        name: "FK_vinculo_whatsapp_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operacao_pendente",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    especie = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tipo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    data_movimentacao = table.Column<DateOnly>(type: "date", nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campo_faltante = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    identificador_mensagem = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    perguntada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operacao_pendente", x => x.id);
                    table.ForeignKey(
                        name: "FK_operacao_pendente_categoria_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categoria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operacao_pendente_conta_financeira_conta_id",
                        column: x => x.conta_id,
                        principalTable: "conta_financeira",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operacao_pendente_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    moeda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    data_movimentacao = table.Column<DateOnly>(type: "date", nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    origem = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    identificador_mensagem = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    excluida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transacao", x => x.id);
                    table.CheckConstraint("ck_transacao_moeda_brl", "moeda = 'BRL'");
                    table.CheckConstraint("ck_transacao_valor_positivo", "valor > 0");
                    table.ForeignKey(
                        name: "FK_transacao_categoria_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categoria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transacao_conta_financeira_conta_id_usuario_id",
                        columns: x => new { x.conta_id, x.usuario_id },
                        principalTable: "conta_financeira",
                        principalColumns: new[] { "id", "usuario_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transacao_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mensagem_processada",
                columns: table => new
                {
                    identificador_externo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    efeito = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    transacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resposta = table.Column<string>(type: "text", nullable: true),
                    recebida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processamento_iniciado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concluida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mensagem_processada", x => x.identificador_externo);
                    table.ForeignKey(
                        name: "FK_mensagem_processada_transacao_transacao_id",
                        column: x => x.transacao_id,
                        principalTable: "transacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mensagem_processada_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "categoria",
                columns: new[] { "id", "criado_em", "escopo", "nome", "tipo", "usuario_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Alimentação", "DESPESA", null },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Mercado", "DESPESA", null },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Transporte", "DESPESA", null },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Moradia", "DESPESA", null },
                    { new Guid("00000000-0000-0000-0000-000000000005"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Saúde", "DESPESA", null },
                    { new Guid("00000000-0000-0000-0000-000000000006"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Educação", "DESPESA", null },
                    { new Guid("00000000-0000-0000-0000-000000000007"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Lazer", "DESPESA", null },
                    { new Guid("00000000-0000-0000-0000-000000000008"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Compras", "DESPESA", null },
                    { new Guid("00000000-0000-0000-0000-000000000009"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Assinaturas", "DESPESA", null },
                    { new Guid("00000000-0000-0000-0000-000000000010"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Outros", "DESPESA", null },
                    { new Guid("00000000-0000-0000-0000-000000000011"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Salário", "RECEITA", null },
                    { new Guid("00000000-0000-0000-0000-000000000012"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Freelance", "RECEITA", null },
                    { new Guid("00000000-0000-0000-0000-000000000013"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Reembolso", "RECEITA", null },
                    { new Guid("00000000-0000-0000-0000-000000000014"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Transferência recebida", "RECEITA", null },
                    { new Guid("00000000-0000-0000-0000-000000000015"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Investimentos", "RECEITA", null },
                    { new Guid("00000000-0000-0000-0000-000000000016"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PADRAO", "Outros", "RECEITA", null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_categoria_padrao_tipo_nome",
                table: "categoria",
                columns: new[] { "tipo", "nome" },
                unique: true,
                filter: "usuario_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_categoria_usuario_tipo_nome",
                table: "categoria",
                columns: new[] { "usuario_id", "tipo", "nome" },
                unique: true,
                filter: "usuario_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_codigo_vinculo_usuario_id",
                table: "codigo_vinculo",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_conta_id_usuario",
                table: "conta_financeira",
                columns: new[] { "id", "usuario_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conta_usuario_nome",
                table: "conta_financeira",
                columns: new[] { "usuario_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conta_usuario_padrao",
                table: "conta_financeira",
                column: "usuario_id",
                unique: true,
                filter: "padrao = true");

            migrationBuilder.CreateIndex(
                name: "IX_mensagem_processada_transacao_id",
                table: "mensagem_processada",
                column: "transacao_id");

            migrationBuilder.CreateIndex(
                name: "IX_mensagem_processada_usuario_id",
                table: "mensagem_processada",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_operacao_pendente_categoria_id",
                table: "operacao_pendente",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "IX_operacao_pendente_conta_id",
                table: "operacao_pendente",
                column: "conta_id");

            migrationBuilder.CreateIndex(
                name: "ix_operacao_pendente_usuario",
                table: "operacao_pendente",
                column: "usuario_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transacao_categoria_id",
                table: "transacao",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "IX_transacao_conta_id_usuario_id",
                table: "transacao",
                columns: new[] { "conta_id", "usuario_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transacao_identificador_mensagem",
                table: "transacao",
                column: "identificador_mensagem",
                unique: true,
                filter: "identificador_mensagem IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_transacao_usuario_categoria",
                table: "transacao",
                columns: new[] { "usuario_id", "categoria_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transacao_usuario_conta",
                table: "transacao",
                columns: new[] { "usuario_id", "conta_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transacao_usuario_data",
                table: "transacao",
                columns: new[] { "usuario_id", "data_movimentacao", "criado_em" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_usuario_email",
                table: "usuario",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_firebase_uid",
                table: "usuario",
                column: "firebase_uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vinculo_numero",
                table: "vinculo_whatsapp",
                column: "numero_e164",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vinculo_usuario",
                table: "vinculo_whatsapp",
                column: "usuario_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "codigo_vinculo");

            migrationBuilder.DropTable(
                name: "contexto_consulta");

            migrationBuilder.DropTable(
                name: "mensagem_processada");

            migrationBuilder.DropTable(
                name: "operacao_pendente");

            migrationBuilder.DropTable(
                name: "vinculo_whatsapp");

            migrationBuilder.DropTable(
                name: "transacao");

            migrationBuilder.DropTable(
                name: "categoria");

            migrationBuilder.DropTable(
                name: "conta_financeira");

            migrationBuilder.DropTable(
                name: "usuario");
        }
    }
}
