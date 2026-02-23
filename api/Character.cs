namespace api;

public record CharacterList(List<Character> characters);
public record Character(string name, string description, int? level, string? race);